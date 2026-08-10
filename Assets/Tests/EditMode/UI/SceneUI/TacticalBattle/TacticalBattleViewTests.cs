using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace Rebellion.Tests.UI.SceneUI.TacticalBattle
{
    [TestFixture]
    public class TacticalBattleViewTests
    {
        private GameObject root;
        private TacticalBattleView view;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("TacticalBattleViewTests");
            root.SetActive(false);
            view = root.AddComponent<TacticalBattleView>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(root);
        }

        [Test]
        public void Awake_CompleteReferences_ForwardsIndexedButtonInput()
        {
            Button[] taskForces = CreateButtons(8, "TaskForce");
            Button[] fighterGroups = CreateButtons(4, "FighterGroup");
            Button[] navigationSets = CreateButtons(4, "NavigationSet");
            RawImage pauseImage = CreateButton("Pause", out Button pauseButton);
            int selectedTaskForce = -1;
            int selectedFighterGroup = -1;
            int selectedNavigationSet = -1;
            bool pauseToggled = false;
            view.Configure(taskForces, fighterGroups, navigationSets, pauseButton, pauseImage);
            view.TaskForceSelected += index => selectedTaskForce = index;
            view.FighterGroupSelected += index => selectedFighterGroup = index;
            view.NavigationSetSelected += index => selectedNavigationSet = index;
            view.PauseToggled += () => pauseToggled = true;
            UIComponentTestHelper.InvokeLifecycle(view, "Awake");

            taskForces[6].onClick.Invoke();
            fighterGroups[2].onClick.Invoke();
            navigationSets[3].onClick.Invoke();
            pauseButton.onClick.Invoke();

            Assert.AreEqual(6, selectedTaskForce);
            Assert.AreEqual(2, selectedFighterGroup);
            Assert.AreEqual(3, selectedNavigationSet);
            Assert.IsTrue(pauseToggled);
        }

        [Test]
        public void Awake_IncorrectTaskForceCount_ThrowsMissingReferenceException()
        {
            RawImage pauseImage = CreateButton("Pause", out Button pauseButton);
            view.Configure(
                CreateButtons(7, "TaskForce"),
                CreateButtons(4, "FighterGroup"),
                CreateButtons(4, "NavigationSet"),
                pauseButton,
                pauseImage
            );

            Assert.Throws<MissingReferenceException>(() =>
                UIComponentTestHelper.InvokeLifecycle(view, "Awake")
            );
        }

        private Button[] CreateButtons(int count, string name)
        {
            Button[] buttons = new Button[count];
            for (int index = 0; index < count; index++)
                CreateButton($"{name}{index + 1}", out buttons[index]);

            return buttons;
        }

        private RawImage CreateButton(string name, out Button button)
        {
            GameObject target = new GameObject(name, typeof(RectTransform), typeof(RawImage));
            target.transform.SetParent(root.transform, false);
            RawImage image = target.GetComponent<RawImage>();
            button = target.AddComponent<Button>();
            button.targetGraphic = image;
            return image;
        }
    }
}
