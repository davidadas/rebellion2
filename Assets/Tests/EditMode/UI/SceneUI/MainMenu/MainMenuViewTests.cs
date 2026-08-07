using System;
using System.Linq;
using System.Reflection;
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
    public class MainMenuViewTests
    {
        private const string _prefabPath = "Assets/Prefabs/UI/MainMenu/MainMenuRoot.prefab";

        private GameObject _prefabRoot;
        private MainMenuView _view;

        [SetUp]
        public void SetUp()
        {
            _prefabRoot = PrefabUtility.LoadPrefabContents(_prefabPath);
            if (_prefabRoot == null)
                throw new InvalidOperationException($"Missing test prefab at {_prefabPath}.");

            _view = _prefabRoot.GetComponentInChildren<MainMenuView>(true);
            UIComponentTestHelper.InvokeLifecycle(_view, "OnEnable");
        }

        [TearDown]
        public void TearDown()
        {
            UIComponentTestHelper.InvokeLifecycle(_view, "OnDisable");
            PrefabUtility.UnloadPrefabContents(_prefabRoot);
        }

        [TestCase(GameVictoryCondition.Conquest, "Standard Game")]
        [TestCase(GameVictoryCondition.Headquarters, "Headquarters Victory")]
        public void RenderVictoryCondition_KnownCondition_AppliesMatchingSpriteAndText(
            GameVictoryCondition condition,
            string expectedText
        )
        {
            Sprite expectedSprite =
                condition == GameVictoryCondition.Headquarters
                    ? GetField<Sprite>("headquartersVictoryConditionSprite")
                    : GetField<Sprite>("standardVictoryConditionSprite");

            _view.RenderVictoryCondition(condition);

            Image icon = GetField<Image>("victoryConditionIcon");
            Assert.AreSame(expectedSprite, icon.sprite);
            Assert.IsTrue(icon.gameObject.activeSelf);
            Assert.AreEqual(expectedText, GetField<TMP_Text>("victoryConditionText").text);
            Assert.AreEqual(
                condition == GameVictoryCondition.Headquarters,
                GetField<GameObject>("victoryConditionSelectionOverlay").activeSelf
            );
            Assert.AreEqual(
                condition != GameVictoryCondition.Headquarters,
                GetField<AutoRotate>("victoryConditionSpinner").enabled
            );
        }

        [Test]
        public void VerifyReferences_AuthoredPrefab_DoesNotThrow()
        {
            Assert.DoesNotThrow(() =>
                UIComponentTestHelper.InvokeLifecycle(_view, "VerifyReferences")
            );
        }

        [Test]
        public void AuthoredPrefab_CockpitBackdropAndControlsShareFullCanvas()
        {
            Transform canvas = _prefabRoot.transform.Find("UI/Canvas");
            Transform viewport = canvas.Find("Viewport");
            Assert.IsNull(canvas.Find("DesignSurface"));
            Assert.IsNotNull(viewport);
            Assert.AreEqual(
                AspectRatioFitter.AspectMode.FitInParent,
                viewport.GetComponent<AspectRatioFitter>().aspectMode
            );
            Assert.IsNotNull(viewport.Find("SpaceBackdrop/Starfield"));
            Assert.IsNull(viewport.Find("SpaceBackdrop/Planet"));
            Assert.IsNotNull(viewport.Find("Cockpit"));
            Assert.AreSame(viewport, viewport.Find("MainMenuControls").parent);
            Assert.AreEqual(6, _prefabRoot.GetComponentsInChildren<AutoRotate>(true).Length);
        }

        [Test]
        public void TryGetSelectedDifficulty_SelectedAuthoredToggle_ReturnsMappedDifficulty()
        {
            Array bindings = GetBindings("difficultyBindings");
            SetAllToggles(bindings, false);
            object selectedBinding = bindings.GetValue(1);
            Toggle selectedToggle = GetBindingValue<Toggle>(selectedBinding, "Toggle");
            GameDifficulty expected = GetBindingValue<GameDifficulty>(selectedBinding, "Value");
            selectedToggle.SetIsOnWithoutNotify(true);

            bool found = _view.TryGetSelectedDifficulty(out GameDifficulty difficulty);

            Assert.IsTrue(found);
            Assert.AreEqual(expected, difficulty);
        }

        [Test]
        public void TryGetSelectedDifficulty_NoSelectedToggle_ReturnsFalse()
        {
            FieldInfo field = typeof(MainMenuView).GetField(
                "difficultyBindings",
                BindingFlags.Instance | BindingFlags.NonPublic
            );
            field.SetValue(_view, Array.CreateInstance(field.FieldType.GetElementType(), 0));

            bool found = _view.TryGetSelectedDifficulty(out GameDifficulty difficulty);

            Assert.IsFalse(found);
            Assert.AreEqual(default(GameDifficulty), difficulty);
        }

        [Test]
        public void CommandButtons_Click_RaiseMatchingSemanticRequests()
        {
            int loadCount = 0;
            int exitCount = 0;
            int creditsCount = 0;
            int victoryCount = 0;
            _view.LoadGameRequested += () => loadCount++;
            _view.ExitRequested += () => exitCount++;
            _view.CreditsRequested += () => creditsCount++;
            _view.VictoryConditionToggleRequested += () => victoryCount++;

            GetField<Button>("loadGameButton").onClick.Invoke();
            GetField<Button>("exitButton").onClick.Invoke();
            GetField<Button>("creditsButton").onClick.Invoke();
            GetField<Button>("victoryConditionButton").onClick.Invoke();

            Assert.AreEqual(1, loadCount);
            Assert.AreEqual(0, exitCount);
            SaveMenuConfirmDialogView confirmation = GetField<SaveMenuConfirmDialogView>(
                "exitConfirmationDialog"
            );
            Assert.IsTrue(confirmation.gameObject.activeSelf);
            confirmation
                .GetComponentsInChildren<Button>(true)
                .Single(button => button.name == "ConfirmButtonImage")
                .onClick.Invoke();
            Assert.AreEqual(1, exitCount);
            Assert.AreEqual(1, creditsCount);
            Assert.AreEqual(1, victoryCount);
        }

        [Test]
        public void GalaxySizeToggle_Selected_RaisesMappedGalaxySize()
        {
            Array bindings = GetBindings("galaxySizeBindings");
            SetAllToggles(bindings, false);
            object selectedBinding = bindings.GetValue(1);
            Toggle toggle = GetBindingValue<Toggle>(selectedBinding, "Toggle");
            GameSize expected = GetBindingValue<GameSize>(selectedBinding, "Value");
            GameSize? selected = null;
            _view.GalaxySizeSelected += value => selected = value;

            toggle.isOn = true;

            Assert.AreEqual(expected, selected);
        }

        [Test]
        public void DifficultyToggle_Selected_RaisesMappedDifficulty()
        {
            Array bindings = GetBindings("difficultyBindings");
            SetAllToggles(bindings, false);
            object selectedBinding = bindings.GetValue(2);
            Toggle toggle = GetBindingValue<Toggle>(selectedBinding, "Toggle");
            GameDifficulty expected = GetBindingValue<GameDifficulty>(selectedBinding, "Value");
            GameDifficulty? selected = null;
            _view.DifficultySelected += value => selected = value;

            toggle.isOn = true;

            Assert.AreEqual(expected, selected);
        }

        [Test]
        public void FactionLaunchButtons_Click_RaiseConfiguredFactionIDs()
        {
            Array bindings = GetBindings("factionLaunchBindings");
            string firstRequestedId = null;
            string secondRequestedId = null;
            int requestIndex = 0;
            _view.StartGameRequested += factionId =>
            {
                if (requestIndex++ == 0)
                    firstRequestedId = factionId;
                else
                    secondRequestedId = factionId;
            };

            GetBindingValue<Button>(bindings.GetValue(0), "Button").onClick.Invoke();
            GetBindingValue<Button>(bindings.GetValue(1), "Button").onClick.Invoke();

            Assert.AreEqual(
                GetBindingValue<string>(bindings.GetValue(0), "FactionId"),
                firstRequestedId
            );
            Assert.AreEqual(
                GetBindingValue<string>(bindings.GetValue(1), "FactionId"),
                secondRequestedId
            );
        }

        [Test]
        public void AudioCue_ConfiguredPointerEvent_RaisesConfiguredResourcePath()
        {
            object binding = GetBindings("audioCueBindings").GetValue(0);
            EventTrigger trigger = GetBindingValue<EventTrigger>(binding, "Trigger");
            EventTriggerType eventType = GetBindingValue<EventTriggerType>(binding, "EventType");
            string expectedPath = GetBindingValue<string>(binding, "ResourcePath");
            string requestedPath = null;
            _view.AudioCueRequested += path => requestedPath = path;

            InvokeTrigger(trigger, eventType);

            Assert.AreEqual(expectedPath, requestedPath);
        }

        [Test]
        public void ExitLever_PointerPress_ShowsAndRestoresPressedVisual()
        {
            Button exitButton = GetField<Button>("exitButton");
            EventTrigger trigger = exitButton.GetComponent<EventTrigger>();
            GameObject pressedImage = GetField<GameObject>("exitPressedImage");
            Image defaultImage = exitButton.targetGraphic as Image;

            InvokeTrigger(trigger, EventTriggerType.PointerDown);
            Assert.IsTrue(pressedImage.activeSelf);
            Assert.IsFalse(defaultImage.enabled);

            InvokeTrigger(trigger, EventTriggerType.PointerUp);
            Assert.IsFalse(pressedImage.activeSelf);
            Assert.IsTrue(defaultImage.enabled);
        }

        [Test]
        public void ExitLever_PointerUp_RaisesExitAudioCue()
        {
            string requestedPath = null;
            _view.AudioCueRequested += path => requestedPath = path;

            InvokeTrigger(
                GetField<Button>("exitButton").GetComponent<EventTrigger>(),
                EventTriggerType.PointerUp
            );

            Assert.AreEqual("Application/MainMenu/Audio/exit-select", requestedPath);
        }

        [Test]
        public void GetAudioCuePaths_AuthoredBindings_ReturnsDistinctConfiguredPaths()
        {
            CollectionAssert.AreEquivalent(
                new[]
                {
                    "Application/MainMenu/Audio/select",
                    "Application/MainMenu/Audio/galaxysize-select",
                    "Application/MainMenu/Audio/exit-select",
                },
                _view.GetAudioCuePaths()
            );
        }

        [Test]
        public void OnEnable_AlreadyBound_DoesNotDuplicateListeners()
        {
            int loadCount = 0;
            _view.LoadGameRequested += () => loadCount++;

            UIComponentTestHelper.InvokeLifecycle(_view, "OnEnable");
            GetField<Button>("loadGameButton").onClick.Invoke();

            Assert.AreEqual(1, loadCount);
        }

        [Test]
        public void OnDisable_BoundView_UnbindsControls()
        {
            int loadCount = 0;
            int cueCount = 0;
            _view.LoadGameRequested += () => loadCount++;
            _view.AudioCueRequested += _ => cueCount++;

            UIComponentTestHelper.InvokeLifecycle(_view, "OnDisable");
            GetField<Button>("loadGameButton").onClick.Invoke();
            InvokeTrigger(
                GetBindingValue<EventTrigger>(
                    GetBindings("audioCueBindings").GetValue(0),
                    "Trigger"
                ),
                GetBindingValue<EventTriggerType>(
                    GetBindings("audioCueBindings").GetValue(0),
                    "EventType"
                )
            );

            Assert.AreEqual(0, loadCount);
            Assert.AreEqual(0, cueCount);
        }

        private Array GetBindings(string fieldName)
        {
            return GetField<Array>(fieldName);
        }

        private T GetField<T>(string fieldName)
        {
            return (T)
                typeof(MainMenuView)
                    .GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
                    .GetValue(_view);
        }

        private static T GetBindingValue<T>(object binding, string propertyName)
        {
            return (T)binding.GetType().GetProperty(propertyName).GetValue(binding);
        }

        private static void SetAllToggles(Array bindings, bool value)
        {
            foreach (object binding in bindings)
                GetBindingValue<Toggle>(binding, "Toggle").SetIsOnWithoutNotify(value);
        }

        private static void InvokeTrigger(EventTrigger trigger, EventTriggerType eventType)
        {
            foreach (EventTrigger.Entry entry in trigger.triggers)
            {
                if (entry != null && entry.eventID == eventType)
                    entry.callback.Invoke(new BaseEventData(null));
            }
        }
    }
}
