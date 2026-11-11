using NUnit.Framework;
using Rebellion.Gameplay;

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
        public void OpenOrders_NewMissionState_UpdatesDiagramWithoutInventingSelectedButton()
        {
            TacticalCombatHudState state = new();
            state.ReplaceContext(TacticalCombatSelectionKind.SingleObject);

            state.OpenOrders(TacticalCombatOrder.AttackCapitalShips);

            Assert.AreEqual(TacticalCombatOrder.AttackCapitalShips, state.PendingOrder);
            Assert.AreEqual("orders-attack-ships", state.DiagramKey);
            Assert.IsFalse(state.IsSelected(TacticalCombatControl.AttackCapitalShips));
        }

        [Test]
        public void OpenOrders_CommandCategory_RetainsMissionEditorState()
        {
            TacticalCombatHudState state = new();
            state.ReplaceContext(TacticalCombatSelectionKind.SingleObject);
            state.OpenOrders(TacticalCombatOrder.AttackCapitalShips);
            state.SelectControl(TacticalCombatControl.AttackFighters);
            state.CloseEditor();
            state.ReplaceContext(TacticalCombatSelectionKind.ObjectGroup);

            state.OpenOrders(TacticalCombatOrder.AttackCapitalShips);

            Assert.AreEqual(TacticalCombatOrder.AttackFighters, state.PendingOrder);
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
        public void OpenTactics_HoldFireOrder_UsesWidgetOffsetWithoutNormalizingIt()
        {
            TacticalCombatHudState state = new();
            state.ReplaceContext(TacticalCombatSelectionKind.SingleObject);

            state.OpenTactics(TacticalCombatOrder.HoldFire, false);

            Assert.AreEqual(TacticalCombatOrder.RightHook, state.GetPendingManeuver());
            Assert.AreEqual("tactics-right-hook", state.DiagramKey);
        }

        [Test]
        public void OpenTactics_ExistingManeuver_RetainsPriorWidgetChoice()
        {
            TacticalCombatHudState state = new();
            state.ReplaceContext(TacticalCombatSelectionKind.SingleObject);
            state.OpenTactics(TacticalCombatOrder.None, false);
            state.SelectControl(TacticalCombatControl.Anvil);
            state.CloseEditor();

            state.OpenTactics(TacticalCombatOrder.Hammer, false);

            Assert.AreEqual(TacticalCombatOrder.Anvil, state.GetPendingManeuver());
        }

        [Test]
        public void SelectControl_AttackMode_ClearsManeuverSelectionAndRetainsIllustration()
        {
            TacticalCombatHudState state = new();
            state.ReplaceContext(TacticalCombatSelectionKind.SingleObject);
            state.OpenTactics(TacticalCombatOrder.None, false);
            state.SelectControl(TacticalCombatControl.Hammer);

            state.SelectControl(TacticalCombatControl.CloseAttack);

            Assert.AreEqual(TacticalCombatOrder.None, state.GetPendingManeuver());
            Assert.AreEqual("tactics-hammer", state.DiagramKey);
            Assert.IsTrue(state.PendingCloseAttack);
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
