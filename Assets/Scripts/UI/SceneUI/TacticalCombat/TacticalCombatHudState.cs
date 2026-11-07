using Rebellion.Gameplay;

/// <summary>
/// Owns right-panel selection and uncommitted order-editor widget state.
/// </summary>
public sealed class TacticalCombatHudState
{
    private TacticalCombatSelectionKind _context;
    private TacticalCombatHudPanel _previous;
    private TacticalCombatControl? _selectedOrder;
    private TacticalCombatControl? _selectedManeuver;
    public TacticalCombatHudPanel Panel { get; private set; } = TacticalCombatHudPanel.Empty;
    public bool IsLocked { get; private set; }
    public TacticalCombatOrder PendingOrder { get; private set; }
    public bool PendingCloseAttack { get; private set; }
    private string _ordersDiagram;
    private string _tacticsDiagram;
    public string DiagramKey =>
        Panel == TacticalCombatHudPanel.Orders ? _ordersDiagram
        : Panel == TacticalCombatHudPanel.Tactics ? _tacticsDiagram
        : null;

    /// <summary>
    /// Receives an actual context replacement while respecting a locked editor or command panel.
    /// </summary>
    /// <param name="context">The newly installed gameplay context.</param>
    public void ReplaceContext(TacticalCombatSelectionKind context)
    {
        _context = context;
        if (context == TacticalCombatSelectionKind.Empty)
        {
            IsLocked = false;
            Select(TacticalCombatHudPanel.Empty);
        }
        else if (Panel is TacticalCombatHudPanel.Orders or TacticalCombatHudPanel.Tactics)
        {
            IsLocked = false;
            ShowContext();
        }
        else if (!IsLocked)
            ShowContext();
    }

    /// <summary>
    /// Opens the command menu while leaving tactical pause ownership unchanged.
    /// </summary>
    public void OpenMenu()
    {
        if (IsLocked)
            return;
        Select(TacticalCombatHudPanel.Commands);
        IsLocked = true;
    }

    /// <summary>
    /// Covers the command menu with its admitted withdrawal confirmation without changing pause.
    /// </summary>
    public void OpenWithdrawalConfirmation()
    {
        Select(TacticalCombatHudPanel.WithdrawalConfirmation);
    }

    /// <summary>
    /// Closes only the withdrawal prompt, retaining the command menu's selection lock.
    /// </summary>
    public void CloseWithdrawalConfirmation()
    {
        Select(TacticalCombatHudPanel.Commands);
    }

    /// <summary>
    /// Releases the command-menu selection lock before withdrawal changes the side's orders.
    /// </summary>
    public void BeginWithdrawal()
    {
        IsLocked = false;
    }

    /// <summary>
    /// Restores the empty right panel when the queued withdrawal command is dispatched.
    /// </summary>
    public void CompleteWithdrawal()
    {
        IsLocked = false;
        Select(TacticalCombatHudPanel.Empty);
    }

    /// <summary>
    /// Closes the command menu and refreshes the current inspection panel.
    /// </summary>
    public void CloseMenu()
    {
        IsLocked = false;
        ShowContext();
    }

    /// <summary>
    /// Opens the mission editor with its context-dependent persisted widget state.
    /// </summary>
    /// <param name="currentOrder">The addressed requested order.</param>
    public void OpenOrders(TacticalCombatOrder currentOrder)
    {
        if (IsLocked)
            return;
        Select(TacticalCombatHudPanel.Orders);
        IsLocked = true;
        if (
            _context
            is TacticalCombatSelectionKind.SingleObject
                or TacticalCombatSelectionKind.FighterGroup
        )
            PendingOrder = currentOrder;
        _selectedOrder = _selectedOrder == OrderControl(PendingOrder) ? _selectedOrder : null;
        _ordersDiagram = OrderDiagram(PendingOrder);
    }

    /// <summary>
    /// Opens the tactics editor without normalizing its retained maneuver-button selection.
    /// </summary>
    /// <param name="currentOrder">The addressed requested order.</param>
    /// <param name="closeAttack">Whether the addressed group requests close engagement.</param>
    public void OpenTactics(TacticalCombatOrder currentOrder, bool closeAttack)
    {
        if (IsLocked)
            return;
        Select(TacticalCombatHudPanel.Tactics);
        IsLocked = true;
        int index = (int)currentOrder;
        if (index > 0 && index < 5)
        {
            _selectedManeuver = ManeuverControl(index);
            _tacticsDiagram = ManeuverDiagram(_selectedManeuver.Value);
        }
        PendingCloseAttack = closeAttack;
    }

    /// <summary>
    /// Updates a mission or tactic widget without dispatching a gameplay order.
    /// </summary>
    /// <param name="control">The pressed editor image.</param>
    public void SelectControl(TacticalCombatControl control)
    {
        if (
            Panel == TacticalCombatHudPanel.Orders
            && ControlOrder(control) is TacticalCombatOrder order
        )
        {
            _selectedOrder = _selectedOrder == control ? null : control;
            PendingOrder = order;
            _ordersDiagram = OrderDiagram(order);
        }
        else if (Panel == TacticalCombatHudPanel.Tactics)
        {
            if (control == TacticalCombatControl.CloseAttack)
            {
                PendingCloseAttack = !PendingCloseAttack;
                _selectedManeuver = null;
            }
            else if (
                control
                is TacticalCombatControl.LeftHook
                    or TacticalCombatControl.RightHook
                    or TacticalCombatControl.Hammer
                    or TacticalCombatControl.Anvil
                    or TacticalCombatControl.Stop
            )
            {
                _selectedManeuver = _selectedManeuver == control ? null : control;
                _tacticsDiagram = ManeuverDiagram(control);
            }
        }
    }

    /// <summary>
    /// Gets the maneuver currently selected for confirmation, including its unselected state.
    /// </summary>
    /// <returns>The pending maneuver or the empty order.</returns>
    public TacticalCombatOrder GetPendingManeuver() =>
        _selectedManeuver.HasValue
            ? ControlOrder(_selectedManeuver.Value) ?? TacticalCombatOrder.None
            : TacticalCombatOrder.None;

    /// <summary>
    /// Returns from an editor after confirmation or cancellation without applying gameplay itself.
    /// </summary>
    public void CloseEditor()
    {
        IsLocked = false;
        Select(_previous);
    }

    /// <summary>
    /// Resolves one editor button's persistent selected drawing state.
    /// </summary>
    /// <param name="control">The addressed semantic control.</param>
    /// <returns>Whether that image is selected in the current editor.</returns>
    public bool IsSelected(TacticalCombatControl control) =>
        Panel == TacticalCombatHudPanel.Orders
            ? _selectedOrder == control
            : Panel == TacticalCombatHudPanel.Tactics
                && (
                    control == TacticalCombatControl.CloseAttack
                        ? PendingCloseAttack
                        : _selectedManeuver == control
                );

    /// <summary>
    /// Selects the inspection surface belonging to the current gameplay context.
    /// </summary>
    private void ShowContext() =>
        Select(
            _context switch
            {
                TacticalCombatSelectionKind.SingleObject
                or TacticalCombatSelectionKind.EnemyInspection
                or TacticalCombatSelectionKind.MultipleObjects =>
                    TacticalCombatHudPanel.SingleObject,
                TacticalCombatSelectionKind.ObjectGroup => TacticalCombatHudPanel.ObjectGroup,
                TacticalCombatSelectionKind.FighterGroup => TacticalCombatHudPanel.FighterGroup,
                _ => TacticalCombatHudPanel.Empty,
            }
        );

    /// <summary>
    /// Preserves the preceding panel when installing a distinct right-panel surface.
    /// </summary>
    /// <param name="panel">The replacement panel.</param>
    private void Select(TacticalCombatHudPanel panel)
    {
        if (Panel != panel)
        {
            _previous = Panel;
            Panel = panel;
        }
    }

    /// <summary>
    /// Resolves the mission image corresponding to a requested order.
    /// </summary>
    /// <param name="order">The requested behavior.</param>
    /// <returns>The matching control, or null for other orders.</returns>
    private static TacticalCombatControl? OrderControl(TacticalCombatOrder order) =>
        order switch
        {
            TacticalCombatOrder.AttackCapitalShips => TacticalCombatControl.AttackCapitalShips,
            TacticalCombatOrder.AttackFighters => TacticalCombatControl.AttackFighters,
            TacticalCombatOrder.Recover => TacticalCombatControl.Recover,
            TacticalCombatOrder.AttackBattleStation => TacticalCombatControl.AttackBattleStation,
            _ => null,
        };

    /// <summary>
    /// Resolves an editor image into its requested behavior.
    /// </summary>
    /// <param name="control">The selected image.</param>
    /// <returns>The behavior, or null for non-order controls.</returns>
    private static TacticalCombatOrder? ControlOrder(TacticalCombatControl control) =>
        control switch
        {
            TacticalCombatControl.AttackCapitalShips => TacticalCombatOrder.AttackCapitalShips,
            TacticalCombatControl.AttackFighters => TacticalCombatOrder.AttackFighters,
            TacticalCombatControl.Recover => TacticalCombatOrder.Recover,
            TacticalCombatControl.AttackBattleStation => TacticalCombatOrder.AttackBattleStation,
            TacticalCombatControl.LeftHook => TacticalCombatOrder.LeftHook,
            TacticalCombatControl.RightHook => TacticalCombatOrder.RightHook,
            TacticalCombatControl.Hammer => TacticalCombatOrder.Hammer,
            TacticalCombatControl.Anvil => TacticalCombatOrder.Anvil,
            TacticalCombatControl.Stop => TacticalCombatOrder.Stop,
            _ => null,
        };

    /// <summary>
    /// Resolves a retained maneuver-button index from the editor's widget ordering.
    /// </summary>
    /// <param name="index">The zero-based maneuver index.</param>
    /// <returns>The corresponding image control.</returns>
    private static TacticalCombatControl ManeuverControl(int index) =>
        index switch
        {
            0 => TacticalCombatControl.LeftHook,
            1 => TacticalCombatControl.RightHook,
            2 => TacticalCombatControl.Hammer,
            3 => TacticalCombatControl.Anvil,
            _ => TacticalCombatControl.Stop,
        };

    /// <summary>
    /// Resolves the mission illustration independently of the image's pressed state.
    /// </summary>
    /// <param name="order">The pending mission.</param>
    /// <returns>The authored illustration key.</returns>
    private static string OrderDiagram(TacticalCombatOrder order) =>
        order switch
        {
            TacticalCombatOrder.AttackCapitalShips => "orders-attack-ships",
            TacticalCombatOrder.AttackFighters => "orders-attack-fighters",
            TacticalCombatOrder.Recover => "orders-recover",
            TacticalCombatOrder.AttackBattleStation => "orders-attack-station",
            _ => "orders-empty",
        };

    /// <summary>
    /// Resolves the illustration belonging to a pressed maneuver image.
    /// </summary>
    /// <param name="control">The maneuver image.</param>
    /// <returns>The authored illustration key.</returns>
    private static string ManeuverDiagram(TacticalCombatControl control) =>
        control switch
        {
            TacticalCombatControl.LeftHook => "tactics-left-hook",
            TacticalCombatControl.RightHook => "tactics-right-hook",
            TacticalCombatControl.Hammer => "tactics-hammer",
            TacticalCombatControl.Anvil => "tactics-anvil",
            _ => "tactics-stop",
        };
}
