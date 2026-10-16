using System;
using System.Globalization;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game;
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
        private GameObject instance;
        private NewGameSettingsView view;
        private GameSummary summary;

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
            summary = new GameSummary { Difficulty = GameDifficulty.Easy };
        }

        /// <summary>
        /// Releases the generated controls.
        /// </summary>
        [TearDown]
        public void TearDown() => UnityEngine.Object.DestroyImmediate(instance);

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
            Assert.IsFalse(ApplyButton().interactable);
        }

        [Test]
        public void Initialize_ExistingOverrides_ShowsOverrideValues()
        {
            summary.PlayerDifficultyModifiers = new DifficultyModifiers
            {
                ManufacturingSpeedPercent = 135,
            };
            summary.AIDifficultyModifiers = new DifficultyModifiers
            {
                ManufacturingSpeedPercent = 165,
            };

            view.Initialize(TestContent.Pack, summary);

            Assert.AreEqual("135", Input("manufacturing-speed", "Player").text);
            Assert.AreEqual("165", Input("manufacturing-speed", "AI").text);
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
                "Detection Rating Bonus",
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
        public void EditValue_PlayerColumn_LeavesAIColumnUnchanged()
        {
            view.Initialize(TestContent.Pack, summary);
            string aiValue = Input("mine-output", "AI").text;

            Input("mine-output", "Player").onEndEdit.Invoke("125");

            Assert.AreEqual("125", Input("mine-output", "Player").text);
            Assert.AreEqual(aiValue, Input("mine-output", "AI").text);
            Assert.AreEqual("1 CHANGE", ChangeCount().text);
            Assert.IsTrue(ApplyButton().interactable);
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
            Assert.IsFalse(ApplyButton().interactable);
        }

        [Test]
        public void EditValue_MissionPointBonus_AcceptsNegativeInteger()
        {
            view.Initialize(TestContent.Pack, summary);

            Input("mission-success", "Player").onEndEdit.Invoke("-15");

            Assert.AreEqual("-15", Input("mission-success", "Player").text);
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
        public void Apply_EditedColumns_PublishesPlayerAndAIModifiers()
        {
            DifficultyModifiers appliedPlayer = null;
            DifficultyModifiers appliedAI = null;
            view.ApplyRequested += (player, ai) =>
            {
                appliedPlayer = player;
                appliedAI = ai;
            };
            view.Initialize(TestContent.Pack, summary);
            Input("manufacturing-speed", "Player").onEndEdit.Invoke("125");
            Input("mission-foil", "AI").onEndEdit.Invoke("20");

            ApplyButton().onClick.Invoke();

            Assert.AreEqual(125, appliedPlayer.ManufacturingSpeedPercent);
            Assert.AreEqual(20, appliedAI.MissionFoilChancePoints);
            Assert.IsFalse(ApplyButton().interactable);
            Assert.AreEqual("0 CHANGES", ChangeCount().text);
        }

        [Test]
        public void Initialize_DifficultyRow_UsesInfoTooltip()
        {
            view.Initialize(TestContent.Pack, summary);
            Transform row = Input("mine-output", "Player").transform.parent;

            Assert.IsNotNull(row.Find("Info").GetComponent<Button>());
            Assert.IsNotNull(row.Find("Info").GetComponent<EventTrigger>());
            Assert.IsNull(row.Find("Description"));
            Assert.IsFalse(
                instance.transform.Find("ContentRoot/SettingTooltip").gameObject.activeSelf
            );
        }

        [TestCase("Player")]
        [TestCase("AI")]
        public void Initialize_DifficultyRow_ResetUsesOptionsSpacing(string column)
        {
            view.Initialize(TestContent.Pack, summary);
            RectTransform field = (RectTransform)Input("mine-output", column).transform;
            RectTransform reset = (RectTransform)field.parent.Find(column + "Reset");

            Assert.AreEqual(
                field.anchoredPosition.x + field.sizeDelta.x + 4f,
                reset.anchoredPosition.x
            );
            Assert.AreEqual(new Vector2(30f, 16f), reset.sizeDelta);
        }

        [Test]
        public void Initialize_WindowHeading_IsCentered()
        {
            view.Initialize(TestContent.Pack, summary);

            Assert.AreEqual(
                TextAlignmentOptions.Midline,
                instance
                    .transform.Find("ContentRoot/HeaderTextField")
                    .GetComponent<TMP_Text>()
                    .alignment
            );
        }

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
        /// Finds the Apply button.
        /// </summary>
        /// <returns>The Apply button.</returns>
        private Button ApplyButton() =>
            instance.transform.Find("ContentRoot/Apply").GetComponent<Button>();

        /// <summary>
        /// Finds the draft change counter.
        /// </summary>
        /// <returns>The change-counter label.</returns>
        private TMP_Text ChangeCount() =>
            instance.transform.Find("ContentRoot/ChangeCount").GetComponent<TMP_Text>();
    }
}
