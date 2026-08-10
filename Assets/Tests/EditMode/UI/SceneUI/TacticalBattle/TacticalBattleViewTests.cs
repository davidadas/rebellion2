using System.Linq;
using NUnit.Framework;
using Rebellion.Game.Tactical;
using UnityEngine;
using UnityEngine.UI;

namespace Rebellion.Tests.UI.SceneUI.TacticalBattle
{
    [TestFixture]
    public class TacticalBattleViewTests
    {
        private GameObject root;
        private TacticalBattleView view;
        private GameObject maneuverPanel;
        private Button[] maneuverButtons;
        private Button formationButton;
        private Button assignManeuverButton;
        private Button cancelManeuverButton;
        private GameObject withdrawalPanel;
        private Button withdrawalButton;
        private Button confirmWithdrawalButton;
        private Button cancelWithdrawalButton;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("TacticalBattleViewTests");
            root.SetActive(false);
            view = root.AddComponent<TacticalBattleView>();
            CreateManeuverControls();
            CreateWithdrawalControls();
            view.ConfigureWithdrawal(
                withdrawalButton,
                withdrawalPanel,
                confirmWithdrawalButton,
                cancelWithdrawalButton
            );
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
            CreateFighterOrderControls(
                out GameObject fighterOrderPanel,
                out Button[] fighterOrders,
                out Button assignFighterOrder,
                out Button cancelFighterOrder
            );
            RawImage pauseImage = CreateButton("Pause", out Button pauseButton);
            int selectedTaskForce = -1;
            int selectedFighterGroup = -1;
            int selectedNavigationSet = -1;
            bool pauseToggled = false;
            view.Configure(
                taskForces,
                fighterGroups,
                navigationSets,
                fighterOrderPanel,
                fighterOrders,
                assignFighterOrder,
                cancelFighterOrder,
                maneuverPanel,
                maneuverButtons,
                formationButton,
                assignManeuverButton,
                cancelManeuverButton,
                pauseButton,
                pauseImage
            );
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
            CreateFighterOrderControls(
                out GameObject fighterOrderPanel,
                out Button[] fighterOrders,
                out Button assignFighterOrder,
                out Button cancelFighterOrder
            );
            RawImage pauseImage = CreateButton("Pause", out Button pauseButton);
            view.Configure(
                CreateButtons(7, "TaskForce"),
                CreateButtons(4, "FighterGroup"),
                CreateButtons(4, "NavigationSet"),
                fighterOrderPanel,
                fighterOrders,
                assignFighterOrder,
                cancelFighterOrder,
                maneuverPanel,
                maneuverButtons,
                formationButton,
                assignManeuverButton,
                cancelManeuverButton,
                pauseButton,
                pauseImage
            );

            Assert.Throws<MissingReferenceException>(() =>
                UIComponentTestHelper.InvokeLifecycle(view, "Awake")
            );
        }

        [Test]
        public void SetGroupAvailability_PopulatedSlots_DisablesEmptySlots()
        {
            Button[] taskForces = CreateButtons(8, "TaskForce");
            Button[] fighterGroups = CreateButtons(4, "FighterGroup");
            CreateFighterOrderControls(
                out GameObject fighterOrderPanel,
                out Button[] fighterOrders,
                out Button assignFighterOrder,
                out Button cancelFighterOrder
            );
            RawImage pauseImage = CreateButton("Pause", out Button pauseButton);
            view.Configure(
                taskForces,
                fighterGroups,
                CreateButtons(4, "NavigationSet"),
                fighterOrderPanel,
                fighterOrders,
                assignFighterOrder,
                cancelFighterOrder,
                maneuverPanel,
                maneuverButtons,
                formationButton,
                assignManeuverButton,
                cancelManeuverButton,
                pauseButton,
                pauseImage
            );

            view.SetGroupAvailability(3, 2);

            Assert.IsTrue(taskForces.Take(3).All(button => button.interactable));
            Assert.IsTrue(taskForces.Skip(3).All(button => !button.interactable));
            Assert.IsTrue(fighterGroups.Take(2).All(button => button.interactable));
            Assert.IsTrue(fighterGroups.Skip(2).All(button => !button.interactable));
        }

        [Test]
        public void Awake_FighterOrderSelection_RaisesPendingOrderBeforeAssignment()
        {
            CreateFighterOrderControls(
                out GameObject fighterOrderPanel,
                out Button[] fighterOrders,
                out Button assignFighterOrder,
                out Button cancelFighterOrder
            );
            RawImage pauseImage = CreateButton("Pause", out Button pauseButton);
            TacticalBehavior selectedOrder = TacticalBehavior.None;
            bool assigned = false;
            view.Configure(
                CreateButtons(8, "TaskForce"),
                CreateButtons(4, "FighterGroup"),
                CreateButtons(4, "NavigationSet"),
                fighterOrderPanel,
                fighterOrders,
                assignFighterOrder,
                cancelFighterOrder,
                maneuverPanel,
                maneuverButtons,
                formationButton,
                assignManeuverButton,
                cancelManeuverButton,
                pauseButton,
                pauseImage
            );
            view.FighterOrderSelected += behavior => selectedOrder = behavior;
            view.FighterOrderAssigned += () => assigned = true;
            UIComponentTestHelper.InvokeLifecycle(view, "Awake");
            view.ShowFighterOrders(true, true);

            fighterOrders[2].onClick.Invoke();

            Assert.AreEqual(TacticalBehavior.AttackDeathStar, selectedOrder);
            Assert.IsFalse(assigned);
            Assert.IsTrue(assignFighterOrder.interactable);
        }

        [Test]
        public void ShowFighterOrders_UnavailableSpecialOrders_DisablesTheirButtons()
        {
            CreateFighterOrderControls(
                out GameObject fighterOrderPanel,
                out Button[] fighterOrders,
                out Button assignFighterOrder,
                out Button cancelFighterOrder
            );
            RawImage pauseImage = CreateButton("Pause", out Button pauseButton);
            view.Configure(
                CreateButtons(8, "TaskForce"),
                CreateButtons(4, "FighterGroup"),
                CreateButtons(4, "NavigationSet"),
                fighterOrderPanel,
                fighterOrders,
                assignFighterOrder,
                cancelFighterOrder,
                maneuverPanel,
                maneuverButtons,
                formationButton,
                assignManeuverButton,
                cancelManeuverButton,
                pauseButton,
                pauseImage
            );

            view.ShowFighterOrders(false, false);

            Assert.IsFalse(fighterOrders[1].interactable);
            Assert.IsFalse(fighterOrders[2].interactable);
            Assert.IsTrue(assignFighterOrder.interactable);
        }

        [Test]
        public void Awake_ManeuverSelection_RaisesPendingValuesBeforeAssignment()
        {
            CreateFighterOrderControls(
                out GameObject fighterOrderPanel,
                out Button[] fighterOrders,
                out Button assignFighterOrder,
                out Button cancelFighterOrder
            );
            RawImage pauseImage = CreateButton("Pause", out Button pauseButton);
            TacticalBehavior selectedManeuver = TacticalBehavior.None;
            TacticalFormation selectedFormation = TacticalFormation.StandOff;
            bool assigned = false;
            view.Configure(
                CreateButtons(8, "TaskForce"),
                CreateButtons(4, "FighterGroup"),
                CreateButtons(4, "NavigationSet"),
                fighterOrderPanel,
                fighterOrders,
                assignFighterOrder,
                cancelFighterOrder,
                maneuverPanel,
                maneuverButtons,
                formationButton,
                assignManeuverButton,
                cancelManeuverButton,
                pauseButton,
                pauseImage
            );
            view.ManeuverSelected += behavior => selectedManeuver = behavior;
            view.FormationSelected += formation => selectedFormation = formation;
            view.ManeuverAssigned += () => assigned = true;
            UIComponentTestHelper.InvokeLifecycle(view, "Awake");
            view.ShowManeuvers(TacticalFormation.StandOff);

            maneuverButtons[3].onClick.Invoke();
            formationButton.onClick.Invoke();

            Assert.AreEqual(TacticalBehavior.Anvil, selectedManeuver);
            Assert.AreEqual(TacticalFormation.Surround, selectedFormation);
            Assert.IsFalse(assigned);
        }

        [Test]
        public void Awake_WithdrawalButton_RaisesWithdrawalRequested()
        {
            ConfigureCompleteView();
            bool requested = false;
            view.WithdrawalRequested += () => requested = true;
            UIComponentTestHelper.InvokeLifecycle(view, "Awake");

            withdrawalButton.onClick.Invoke();

            Assert.IsTrue(requested);
        }

        [Test]
        public void Awake_ConfirmWithdrawalButton_RaisesWithdrawalConfirmed()
        {
            ConfigureCompleteView();
            bool confirmed = false;
            view.WithdrawalConfirmed += () => confirmed = true;
            UIComponentTestHelper.InvokeLifecycle(view, "Awake");

            confirmWithdrawalButton.onClick.Invoke();

            Assert.IsTrue(confirmed);
        }

        [Test]
        public void Awake_CancelWithdrawalButton_RaisesWithdrawalCancelled()
        {
            ConfigureCompleteView();
            bool cancelled = false;
            view.WithdrawalCancelled += () => cancelled = true;
            UIComponentTestHelper.InvokeLifecycle(view, "Awake");

            cancelWithdrawalButton.onClick.Invoke();

            Assert.IsTrue(cancelled);
        }

        [Test]
        public void ShowWithdrawalConfirmation_OpenPanel_ClosesOtherCommandPanels()
        {
            ConfigureCompleteView();
            UIComponentTestHelper.InvokeLifecycle(view, "Awake");
            view.ShowFighterOrders(true, true);
            view.ShowManeuvers(TacticalFormation.StandOff);

            view.ShowWithdrawalConfirmation();

            Assert.IsTrue(withdrawalPanel.activeSelf);
            Assert.IsFalse(maneuverPanel.activeSelf);
        }

        private Button[] CreateButtons(int count, string name)
        {
            Button[] buttons = new Button[count];
            for (int index = 0; index < count; index++)
                CreateButton($"{name}{index + 1}", out buttons[index]);

            return buttons;
        }

        private void CreateFighterOrderControls(
            out GameObject panel,
            out Button[] orders,
            out Button assign,
            out Button cancel
        )
        {
            panel = new GameObject("FighterOrders", typeof(RectTransform));
            panel.transform.SetParent(root.transform, false);
            orders = CreateButtons(4, "FighterOrder");
            CreateButton("AssignFighterOrder", out assign);
            CreateButton("CancelFighterOrder", out cancel);
        }

        private void CreateManeuverControls()
        {
            maneuverPanel = new GameObject("Maneuvers", typeof(RectTransform));
            maneuverPanel.transform.SetParent(root.transform, false);
            maneuverButtons = CreateButtons(5, "Maneuver");
            CreateButton("Formation", out formationButton);
            CreateButton("AssignManeuver", out assignManeuverButton);
            CreateButton("CancelManeuver", out cancelManeuverButton);
        }

        private void CreateWithdrawalControls()
        {
            withdrawalPanel = new GameObject("Withdrawal", typeof(RectTransform));
            withdrawalPanel.transform.SetParent(root.transform, false);
            CreateButton("Withdraw", out withdrawalButton);
            CreateButton("ConfirmWithdrawal", out confirmWithdrawalButton);
            CreateButton("CancelWithdrawal", out cancelWithdrawalButton);
        }

        private void ConfigureCompleteView()
        {
            CreateFighterOrderControls(
                out GameObject fighterOrderPanel,
                out Button[] fighterOrders,
                out Button assignFighterOrder,
                out Button cancelFighterOrder
            );
            RawImage pauseImage = CreateButton("Pause", out Button pauseButton);
            view.Configure(
                CreateButtons(8, "TaskForce"),
                CreateButtons(4, "FighterGroup"),
                CreateButtons(4, "NavigationSet"),
                fighterOrderPanel,
                fighterOrders,
                assignFighterOrder,
                cancelFighterOrder,
                maneuverPanel,
                maneuverButtons,
                formationButton,
                assignManeuverButton,
                cancelManeuverButton,
                pauseButton,
                pauseImage
            );
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
