using System;
using Rebellion.Tactical;

namespace Rebellion.UI.SceneUI.TacticalCombat
{
    public sealed partial class TacticalCombatController : IDisposable
    {
        /// <summary>
        /// Refreshes navigation markers for the current gameplay selection.
        /// </summary>
        private void RefreshNavigationSelection() =>
            _view.Renderer.UpdateNavigationSelection(_selectedGroups);

        /// <summary>
        /// Displays battle progress and selected-unit condition without allocating text every frame.
        /// </summary>
        private void RefreshHud()
        {
            _view.SetPanel(_hud.Panel);
            if (
                _hud.Panel
                    is TacticalCombatHudPanel.SingleObject
                        or TacticalCombatHudPanel.ObjectGroup
                && _session.Selection.PrimaryUnit is TacticalCombatUnit inspected
            )
                _view.SetInspection(inspected, _inspection.ReadUnit(inspected));
            if (
                _hud.Panel == TacticalCombatHudPanel.FighterGroup
                && _session.Selection.Group != null
            )
                _view.SetFighterInspection(
                    _inspection.ReadFighters(
                        _session.Selection.Group,
                        _session.Selection.Category,
                        _session.GetSide(_session.PlayerSide).FactionIndex
                    )
                );
            _view.SetDiagram(_hud.DiagramKey);
            _view.SetStationCharge(_stationControlsVisible, _session.BattleStation?.Charge ?? 0);
            _view.SetControlSelected(
                TacticalCombatControl.Superlaser,
                _session.Selection.Kind == TacticalCombatSelectionKind.StationTarget
            );
            for (int index = 0; index < 8; index++)
                _view.SetControlSelected(
                    (TacticalCombatControl)((int)TacticalCombatControl.ObjectGroup1 + index),
                    _session.Selection.Kind == TacticalCombatSelectionKind.ObjectGroup
                        && _session.Selection.Category == index,
                    _session.GetObjectGroup(_session.PlayerSide, index) != null
                );
            for (int index = 0; index < 4; index++)
            {
                _view.SetControlSelected(
                    (TacticalCombatControl)((int)TacticalCombatControl.FighterGroup1 + index),
                    _session.Selection.Kind == TacticalCombatSelectionKind.FighterGroup
                        && _session.Selection.Category == index
                );
                _view.SetControlSelected(
                    (TacticalCombatControl)((int)TacticalCombatControl.NavigationRing1 + index),
                    _navigationSelected[index]
                );
            }
            _view.SetControlSelected(
                TacticalCombatControl.Pause,
                _session.IsPaused || _view.Renderer.IsPresentingArrival
            );
            _view.SetControlSelected(
                TacticalCombatControl.ToggleAutomation,
                _session.GetSide(_session.PlayerSide).IsAutomated
            );
            _view.SetControlSelected(
                TacticalCombatControl.HighlightAlliance,
                _factionHighlighted[0]
            );
            _view.SetControlSelected(TacticalCombatControl.HighlightEmpire, _factionHighlighted[1]);
            foreach (TacticalCombatHudControl definition in TacticalCombatHudLayout.Controls)
                if (
                    definition.Panel
                    is TacticalCombatHudPanel.Orders
                        or TacticalCombatHudPanel.Tactics
                )
                    _view.SetControlSelected(
                        definition.Control,
                        _hud.IsSelected(definition.Control)
                    );
        }

        /// <summary>
        /// Projects the arrival pause image independently of the user's current pause state.
        /// </summary>
        /// <param name="active">Whether the arrival sequence currently owns the pause strobe.</param>
        private void HandleArrivalStrobe(bool active) =>
            _view.SetControlSelected(TacticalCombatControl.Pause, active || _session.IsPaused);

        /// <summary>
        /// Projects context-specific controls without adding actions to unrelated inspection surfaces.
        /// </summary>
        private void RefreshControls()
        {
            bool active =
                _ready
                && !_applicationMenuOpen
                && !_session.IsComplete
                && _session.PendingAttackRunMovie == null;
            foreach (TacticalCombatHudControl definition in TacticalCombatHudLayout.Controls)
                _view.SetControlEnabled(definition.Control, active);
            for (int category = 0; category < 4; category++)
                _view.SetControlEnabled(
                    (TacticalCombatControl)((int)TacticalCombatControl.FighterGroup1 + category),
                    active && _session.GetFighterGroup(_session.PlayerSide, category) != null
                );
            _view.SetControlEnabled(
                TacticalCombatControl.OpenOrders,
                active && _session.Selection.CanIssueCommands
            );
            _view.SetControlEnabled(
                TacticalCombatControl.OpenTactics,
                active && _session.Selection.CanIssueCommands
            );
            _view.SetControlEnabled(
                TacticalCombatControl.Recover,
                active && _session.Selection.Kind == TacticalCombatSelectionKind.FighterGroup
            );
            _view.SetControlEnabled(
                TacticalCombatControl.AttackBattleStation,
                active
                    && _session.Selection.Kind == TacticalCombatSelectionKind.FighterGroup
                    && _session.CanAttackBattleStation(_session.PlayerSide)
            );
            _view.SetControlEnabled(
                TacticalCombatControl.Withdraw,
                active && _session.CanWithdraw(_session.PlayerSide)
            );
            _view.SetControlEnabled(
                TacticalCombatControl.Superlaser,
                active && _session.IsDirectControlEnabled && _session.BattleStation?.Charge == 100
            );
            _view.SetControlEnabled(
                TacticalCombatControl.ToggleAutomation,
                active && !_session.GetSide(_session.PlayerSide).IsWithdrawing
            );
            _view.SetControlEnabled(
                TacticalCombatControl.ResolveImmediately,
                active && _session.CanRequestImmediateResult
            );
        }
    }
}
