using NUnit.Framework;
using Rebellion.Gameplay;
using Rebellion.Tactical;

namespace Rebellion.Tests.UI.SceneUI.TacticalCombat
{
    [TestFixture]
    public sealed class TacticalCombatHudStateTests
    {
        [Test]
        public void ReplaceContext_NonemptyWhileEditorLocked_ReleasesEditorAndShowsInspection()
        {
            TacticalCombatHudState state = new();
            state.ReplaceContext(TacticalCombatSelectionKind.SingleObject);
            state.OpenOrders(TacticalCombatOrder.None);

            state.ReplaceContext(TacticalCombatSelectionKind.ObjectGroup);

            Assert.AreEqual(TacticalCombatHudPanel.ObjectGroup, state.Panel);
            Assert.IsFalse(state.IsLocked);
        }

        [Test]
        public void ReplaceContext_NonemptyWhileMenuLocked_PreservesCommandMenu()
        {
            TacticalCombatHudState state = new();
            state.ReplaceContext(TacticalCombatSelectionKind.SingleObject);
            state.OpenMenu();

            state.ReplaceContext(TacticalCombatSelectionKind.ObjectGroup);

            Assert.AreEqual(TacticalCombatHudPanel.Commands, state.Panel);
            Assert.IsTrue(state.IsLocked);
        }

        [Test]
        public void ReplaceContext_EmptyWhileEditorLocked_UnlocksAndShowsBackground()
        {
            TacticalCombatHudState state = new();
            state.ReplaceContext(TacticalCombatSelectionKind.SingleObject);
            state.OpenOrders(TacticalCombatOrder.None);

            state.ReplaceContext(TacticalCombatSelectionKind.Empty);

            Assert.AreEqual(TacticalCombatHudPanel.Empty, state.Panel);
            Assert.IsFalse(state.IsLocked);
        }

        [Test]
        public void OpenWithdrawalConfirmation_CommandMenu_CoversMenuWithoutReleasingItsLock()
        {
            TacticalCombatHudState state = new();
            state.ReplaceContext(TacticalCombatSelectionKind.SingleObject);
            state.OpenMenu();

            state.OpenWithdrawalConfirmation();

            Assert.AreEqual(TacticalCombatHudPanel.WithdrawalConfirmation, state.Panel);
            Assert.IsTrue(state.IsLocked);
        }

        [Test]
        public void CloseWithdrawalConfirmation_Confirmation_ReturnsToLockedCommandMenu()
        {
            TacticalCombatHudState state = new();
            state.OpenMenu();
            state.OpenWithdrawalConfirmation();

            state.CloseWithdrawalConfirmation();

            Assert.AreEqual(TacticalCombatHudPanel.Commands, state.Panel);
            Assert.IsTrue(state.IsLocked);
        }

        [Test]
        public void ReplaceContext_NonemptyWhileConfirming_PreservesWithdrawalPrompt()
        {
            TacticalCombatHudState state = new();
            state.ReplaceContext(TacticalCombatSelectionKind.SingleObject);
            state.OpenMenu();
            state.OpenWithdrawalConfirmation();

            state.ReplaceContext(TacticalCombatSelectionKind.ObjectGroup);

            Assert.AreEqual(TacticalCombatHudPanel.WithdrawalConfirmation, state.Panel);
            Assert.IsTrue(state.IsLocked);
        }

        [Test]
        public void ReplaceContext_EmptyWhileConfirming_ClosesPromptAndUnlocksBackground()
        {
            TacticalCombatHudState state = new();
            state.OpenMenu();
            state.OpenWithdrawalConfirmation();

            state.ReplaceContext(TacticalCombatSelectionKind.Empty);

            Assert.AreEqual(TacticalCombatHudPanel.Empty, state.Panel);
            Assert.IsFalse(state.IsLocked);
        }

        [Test]
        public void BeginWithdrawal_BeforeSideMutation_UnlocksWithoutChangingCommandPanel()
        {
            TacticalCombatHudState state = new();
            state.OpenMenu();

            state.BeginWithdrawal();

            Assert.AreEqual(TacticalCombatHudPanel.Commands, state.Panel);
            Assert.IsFalse(state.IsLocked);
        }

        [Test]
        public void CompleteWithdrawal_QueuedDispatch_ClosesCommandMenuAndUnlocksBackground()
        {
            TacticalCombatHudState state = new();
            state.OpenMenu();
            state.OpenWithdrawalConfirmation();
            state.CloseWithdrawalConfirmation();

            state.CompleteWithdrawal();

            Assert.AreEqual(TacticalCombatHudPanel.Empty, state.Panel);
            Assert.IsFalse(state.IsLocked);
        }

        [Test]
        public void OpenOrders_CurrentMission_SelectsMatchingButtonWithoutPendingChanges()
        {
            TacticalCombatHudState state = new();
            state.ReplaceContext(TacticalCombatSelectionKind.SingleObject);

            state.OpenOrders(TacticalCombatOrder.AttackCapitalShips);

            Assert.AreEqual(TacticalCombatOrder.AttackCapitalShips, state.PendingOrder);
            Assert.AreEqual("orders-attack-ships", state.DiagramKey);
            Assert.IsTrue(state.IsSelected(TacticalCombatControl.AttackCapitalShips));
            Assert.IsFalse(state.HasOrderChanges);
        }

        [Test]
        public void OpenOrders_DifferentCommandCategory_UsesAddressedMission()
        {
            TacticalCombatHudState state = new();
            state.ReplaceContext(TacticalCombatSelectionKind.SingleObject);
            state.OpenOrders(TacticalCombatOrder.AttackCapitalShips);
            state.SelectControl(TacticalCombatControl.AttackFighters);
            state.CloseEditor();
            state.ReplaceContext(TacticalCombatSelectionKind.ObjectGroup);

            state.OpenOrders(TacticalCombatOrder.AttackCapitalShips);

            Assert.AreEqual(TacticalCombatOrder.AttackCapitalShips, state.PendingOrder);
            Assert.IsTrue(state.IsSelected(TacticalCombatControl.AttackCapitalShips));
            Assert.IsFalse(state.HasOrderChanges);
        }

        [Test]
        public void SelectControl_SelectedMissionPressedAgain_RetainsMissionWhileClearingStrobe()
        {
            TacticalCombatHudState state = new();
            state.ReplaceContext(TacticalCombatSelectionKind.SingleObject);
            state.OpenOrders(TacticalCombatOrder.None);
            state.SelectControl(TacticalCombatControl.AttackFighters);

            state.SelectControl(TacticalCombatControl.AttackFighters);

            Assert.AreEqual(TacticalCombatOrder.AttackFighters, state.PendingOrder);
            Assert.IsFalse(state.IsSelected(TacticalCombatControl.AttackFighters));
        }

        [Test]
        public void OpenTactics_MissionWithoutManeuver_LeavesManeuverUnselected()
        {
            TacticalCombatHudState state = new();
            state.ReplaceContext(TacticalCombatSelectionKind.SingleObject);

            state.OpenTactics(TacticalCombatOrder.HoldFire, false);

            Assert.AreEqual(TacticalCombatOrder.None, state.GetPendingManeuver());
            Assert.IsNull(state.DiagramKey);
            Assert.IsFalse(state.HasManeuverChanges);
            Assert.IsFalse(state.HasAttackModeChanges);
        }

        [Test]
        public void OpenTactics_DifferentGroupManeuver_ReplacesPriorWidgetChoice()
        {
            TacticalCombatHudState state = new();
            state.ReplaceContext(TacticalCombatSelectionKind.SingleObject);
            state.OpenTactics(TacticalCombatOrder.None, false);
            state.SelectControl(TacticalCombatControl.Anvil);
            state.CloseEditor();

            state.OpenTactics(TacticalCombatOrder.Hammer, false);

            Assert.AreEqual(TacticalCombatOrder.Hammer, state.GetPendingManeuver());
            Assert.IsFalse(state.HasManeuverChanges);
        }

        [Test]
        public void SelectControl_AttackMode_PreservesManeuverSelectionAndIllustration()
        {
            TacticalCombatHudState state = new();
            state.ReplaceContext(TacticalCombatSelectionKind.SingleObject);
            state.OpenTactics(TacticalCombatOrder.None, false);
            state.SelectControl(TacticalCombatControl.Hammer);

            state.SelectControl(TacticalCombatControl.CloseAttack);

            Assert.AreEqual(TacticalCombatOrder.Hammer, state.GetPendingManeuver());
            Assert.AreEqual("tactics-hammer", state.DiagramKey);
            Assert.IsTrue(state.PendingCloseAttack);
            Assert.IsTrue(state.HasAttackModeChanges);
        }

        [TestCase(TacticalCombatOrder.LeftHook, TacticalCombatControl.LeftHook)]
        [TestCase(TacticalCombatOrder.RightHook, TacticalCombatControl.RightHook)]
        [TestCase(TacticalCombatOrder.Hammer, TacticalCombatControl.Hammer)]
        [TestCase(TacticalCombatOrder.Anvil, TacticalCombatControl.Anvil)]
        [TestCase(TacticalCombatOrder.Stop, TacticalCombatControl.Stop)]
        public void OpenTactics_CurrentManeuver_SelectsCorrespondingControl(
            TacticalCombatOrder order,
            TacticalCombatControl control
        )
        {
            TacticalCombatHudState state = new();
            state.OpenTactics(order, true);

            Assert.AreEqual(order, state.GetPendingManeuver());
            Assert.IsTrue(state.IsSelected(control));
            Assert.IsTrue(state.PendingCloseAttack);
            Assert.IsFalse(state.HasManeuverChanges);
            Assert.IsFalse(state.HasAttackModeChanges);
        }

        [Test]
        public void SelectControl_AttackModeOnly_LeavesManeuverUnchanged()
        {
            TacticalCombatHudState state = new();
            state.OpenTactics(TacticalCombatOrder.AttackFighters, false);

            state.SelectControl(TacticalCombatControl.CloseAttack);

            Assert.IsTrue(state.PendingCloseAttack);
            Assert.IsTrue(state.HasAttackModeChanges);
            Assert.IsFalse(state.HasManeuverChanges);
        }

        [Test]
        public void CloseEditor_PendingChoice_RestoresPreviousInspectionPanel()
        {
            TacticalCombatHudState state = new();
            state.ReplaceContext(TacticalCombatSelectionKind.FighterGroup);
            state.OpenOrders(TacticalCombatOrder.AttackFighters);
            state.SelectControl(TacticalCombatControl.Recover);

            state.CloseEditor();

            Assert.AreEqual(TacticalCombatHudPanel.FighterGroup, state.Panel);
            Assert.IsFalse(state.IsLocked);
        }

        [Test]
        public void CloseMenu_ContextChangedWhileLocked_ReturnsToCurrentInspection()
        {
            TacticalCombatHudState state = new();
            state.ReplaceContext(TacticalCombatSelectionKind.SingleObject);
            state.OpenMenu();
            state.ReplaceContext(TacticalCombatSelectionKind.FighterGroup);

            state.CloseMenu();

            Assert.AreEqual(TacticalCombatHudPanel.FighterGroup, state.Panel);
        }
    }
}
