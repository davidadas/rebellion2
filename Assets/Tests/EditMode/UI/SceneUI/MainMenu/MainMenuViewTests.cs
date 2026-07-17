using System;
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
            int creditsCount = 0;
            int victoryCount = 0;
            _view.LoadGameRequested += () => loadCount++;
            _view.CreditsRequested += () => creditsCount++;
            _view.VictoryConditionToggleRequested += () => victoryCount++;

            GetField<Button>("loadGameButton").onClick.Invoke();
            GetField<Button>("creditsButton").onClick.Invoke();
            GetField<Button>("victoryConditionButton").onClick.Invoke();

            Assert.AreEqual(1, loadCount);
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
        public void FactionLaunchButtons_Click_RaiseConfiguredFactionIds()
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
        public void PressVisual_PointerLifecycle_AppliesAndRestoresAuthoredTargets()
        {
            object binding = GetBindings("pressVisualBindings").GetValue(3);
            EventTrigger trigger = GetBindingValue<EventTrigger>(binding, "Trigger");
            System.Collections.Generic.IReadOnlyList<Graphic> graphics =
                GetBindingValue<System.Collections.Generic.IReadOnlyList<Graphic>>(
                    binding,
                    "GraphicsHiddenWhilePressed"
                );
            System.Collections.Generic.IReadOnlyList<GameObject> objects =
                GetBindingValue<System.Collections.Generic.IReadOnlyList<GameObject>>(
                    binding,
                    "ObjectsShownWhilePressed"
                );

            InvokeTrigger(trigger, EventTriggerType.PointerDown);

            Assert.That(graphics, Has.All.Matches<Graphic>(graphic => !graphic.enabled));
            Assert.That(objects, Has.All.Matches<GameObject>(item => item.activeSelf));

            InvokeTrigger(trigger, EventTriggerType.PointerExit);

            Assert.That(graphics, Has.All.Matches<Graphic>(graphic => graphic.enabled));
            Assert.That(objects, Has.All.Matches<GameObject>(item => !item.activeSelf));
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
        public void OnEnable_AlreadyBound_DoesNotDuplicateListeners()
        {
            int loadCount = 0;
            _view.LoadGameRequested += () => loadCount++;

            UIComponentTestHelper.InvokeLifecycle(_view, "OnEnable");
            GetField<Button>("loadGameButton").onClick.Invoke();

            Assert.AreEqual(1, loadCount);
        }

        [Test]
        public void OnDisable_BoundView_UnbindsControlsAndRestoresPressedPresentation()
        {
            object pressBinding = GetBindings("pressVisualBindings").GetValue(3);
            EventTrigger trigger = GetBindingValue<EventTrigger>(pressBinding, "Trigger");
            System.Collections.Generic.IReadOnlyList<Graphic> graphics =
                GetBindingValue<System.Collections.Generic.IReadOnlyList<Graphic>>(
                    pressBinding,
                    "GraphicsHiddenWhilePressed"
                );
            System.Collections.Generic.IReadOnlyList<GameObject> objects =
                GetBindingValue<System.Collections.Generic.IReadOnlyList<GameObject>>(
                    pressBinding,
                    "ObjectsShownWhilePressed"
                );
            int loadCount = 0;
            int cueCount = 0;
            _view.LoadGameRequested += () => loadCount++;
            _view.AudioCueRequested += _ => cueCount++;
            InvokeTrigger(trigger, EventTriggerType.PointerDown);
            cueCount = 0;

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
            Assert.That(graphics, Has.All.Matches<Graphic>(graphic => graphic.enabled));
            Assert.That(objects, Has.All.Matches<GameObject>(item => !item.activeSelf));
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
