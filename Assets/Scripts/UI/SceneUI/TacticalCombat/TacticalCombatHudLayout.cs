using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Identifies a tactical panel independently of the selected gameplay object.
/// </summary>
public enum TacticalCombatHudPanel
{
    Frame,
    Empty,
    SingleObject,
    ObjectGroup,
    FighterGroup,
    Orders,
    Tactics,
    Commands,
    WithdrawalConfirmation,
}

/// <summary>
/// Describes one addressed image button in the tactical frame or right panel.
/// </summary>
public readonly struct TacticalCombatHudControl
{
    public TacticalCombatControl Control { get; }
    public TacticalCombatHudPanel Panel { get; }
    public RectInt Bounds { get; }
    public string ArtworkKey { get; }
    public string Tooltip { get; }
    public int RepeatMilliseconds { get; }
    public string Label { get; }
    public bool BitmapHitTest { get; }

    /// <summary>
    /// Captures a control's source-space geometry, imagery, and semantic action.
    /// </summary>
    /// <param name="control">The semantic action.</param>
    /// <param name="panel">The owning surface.</param>
    /// <param name="bounds">The pixel bounds within that surface.</param>
    /// <param name="artworkKey">The shared content image key.</param>
    /// <param name="tooltip">The hover description.</param>
    /// <param name="repeatMilliseconds">The held-button repeat interval, or zero.</param>
    /// <param name="label">The optional overlaid category label.</param>
    /// <param name="bitmapHitTest">Whether the fixed up-image mask owns pointer hit testing.</param>
    public TacticalCombatHudControl(
        TacticalCombatControl control,
        TacticalCombatHudPanel panel,
        RectInt bounds,
        string artworkKey,
        string tooltip,
        int repeatMilliseconds,
        string label,
        bool bitmapHitTest
    )
    {
        Control = control;
        Panel = panel;
        Bounds = bounds;
        ArtworkKey = artworkKey;
        Tooltip = tooltip;
        RepeatMilliseconds = repeatMilliseconds;
        Label = label;
        BitmapHitTest = bitmapHitTest;
    }
}

/// <summary>
/// Provides the tactical frame's pixel geometry and explicit semantic control bindings.
/// </summary>
public static class TacticalCombatHudLayout
{
    public static readonly RectInt Viewport = new(17, 30, 439, 437);
    public static readonly RectInt RightPanel = new(482, 28, 149, 236);
    public static IReadOnlyList<TacticalCombatHudControl> Controls { get; } =
        System.Array.AsReadOnly(
            new TacticalCombatHudControl[]
            {
                new(
                    TacticalCombatControl.ObjectGroup1,
                    TacticalCombatHudPanel.Frame,
                    new RectInt(60, 2, 25, 17),
                    "object-slot",
                    "Task force 1",
                    0,
                    "1",
                    false
                ),
                new(
                    TacticalCombatControl.ObjectGroup2,
                    TacticalCombatHudPanel.Frame,
                    new RectInt(89, 2, 25, 17),
                    "object-slot",
                    "Task force 2",
                    0,
                    "2",
                    false
                ),
                new(
                    TacticalCombatControl.ObjectGroup3,
                    TacticalCombatHudPanel.Frame,
                    new RectInt(118, 2, 25, 17),
                    "object-slot",
                    "Task force 3",
                    0,
                    "3",
                    false
                ),
                new(
                    TacticalCombatControl.ObjectGroup4,
                    TacticalCombatHudPanel.Frame,
                    new RectInt(147, 2, 25, 17),
                    "object-slot",
                    "Task force 4",
                    0,
                    "4",
                    false
                ),
                new(
                    TacticalCombatControl.ObjectGroup5,
                    TacticalCombatHudPanel.Frame,
                    new RectInt(176, 2, 25, 17),
                    "object-slot",
                    "Task force 5",
                    0,
                    "5",
                    false
                ),
                new(
                    TacticalCombatControl.ObjectGroup6,
                    TacticalCombatHudPanel.Frame,
                    new RectInt(205, 2, 25, 17),
                    "object-slot",
                    "Task force 6",
                    0,
                    "6",
                    false
                ),
                new(
                    TacticalCombatControl.ObjectGroup7,
                    TacticalCombatHudPanel.Frame,
                    new RectInt(234, 2, 25, 17),
                    "object-slot",
                    "Task force 7",
                    0,
                    "7",
                    false
                ),
                new(
                    TacticalCombatControl.ObjectGroup8,
                    TacticalCombatHudPanel.Frame,
                    new RectInt(263, 2, 25, 17),
                    "object-slot",
                    "Task force 8",
                    0,
                    "8",
                    false
                ),
                new(
                    TacticalCombatControl.FighterGroup1,
                    TacticalCombatHudPanel.Frame,
                    new RectInt(347, 2, 25, 15),
                    "fighter-1",
                    "Red fighter group",
                    0,
                    null,
                    false
                ),
                new(
                    TacticalCombatControl.FighterGroup2,
                    TacticalCombatHudPanel.Frame,
                    new RectInt(376, 2, 25, 15),
                    "fighter-2",
                    "Blue fighter group",
                    0,
                    null,
                    false
                ),
                new(
                    TacticalCombatControl.FighterGroup3,
                    TacticalCombatHudPanel.Frame,
                    new RectInt(405, 2, 25, 15),
                    "fighter-3",
                    "Green fighter group",
                    0,
                    null,
                    false
                ),
                new(
                    TacticalCombatControl.FighterGroup4,
                    TacticalCombatHudPanel.Frame,
                    new RectInt(434, 2, 25, 15),
                    "fighter-4",
                    "Gold fighter group",
                    0,
                    null,
                    false
                ),
                new(
                    TacticalCombatControl.NavigationRing1,
                    TacticalCombatHudPanel.Frame,
                    new RectInt(485, 272, 27, 27),
                    "navigation-1",
                    "Navigation point set 1",
                    0,
                    null,
                    true
                ),
                new(
                    TacticalCombatControl.NavigationRing2,
                    TacticalCombatHudPanel.Frame,
                    new RectInt(524, 272, 27, 27),
                    "navigation-2",
                    "Navigation point set 2",
                    0,
                    null,
                    true
                ),
                new(
                    TacticalCombatControl.NavigationRing3,
                    TacticalCombatHudPanel.Frame,
                    new RectInt(563, 272, 27, 27),
                    "navigation-3",
                    "Navigation point set 3",
                    0,
                    null,
                    true
                ),
                new(
                    TacticalCombatControl.NavigationRing4,
                    TacticalCombatHudPanel.Frame,
                    new RectInt(602, 272, 27, 27),
                    "navigation-4",
                    "Navigation point set 4",
                    0,
                    null,
                    true
                ),
                new(
                    TacticalCombatControl.HighlightAlliance,
                    TacticalCombatHudPanel.Frame,
                    new RectInt(515, 305, 30, 26),
                    "highlight-alliance",
                    "Highlight Alliance ships",
                    0,
                    null,
                    false
                ),
                new(
                    TacticalCombatControl.HighlightEmpire,
                    TacticalCombatHudPanel.Frame,
                    new RectInt(483, 305, 30, 26),
                    "highlight-empire",
                    "Highlight Empire ships",
                    0,
                    null,
                    true
                ),
                new(
                    TacticalCombatControl.OpenMenu,
                    TacticalCombatHudPanel.Frame,
                    new RectInt(608, 309, 20, 20),
                    "menu",
                    "Game options",
                    0,
                    null,
                    true
                ),
                new(
                    TacticalCombatControl.Pause,
                    TacticalCombatHudPanel.Frame,
                    new RectInt(561, 308, 28, 21),
                    "pause",
                    "Pause battle",
                    0,
                    null,
                    true
                ),
                new(
                    TacticalCombatControl.CameraZoomIn,
                    TacticalCombatHudPanel.Frame,
                    new RectInt(491, 343, 24, 24),
                    "zoom-in",
                    "Zoom in",
                    100,
                    null,
                    true
                ),
                new(
                    TacticalCombatControl.CameraZoomOut,
                    TacticalCombatHudPanel.Frame,
                    new RectInt(607, 343, 24, 24),
                    "zoom-out",
                    "Zoom out",
                    100,
                    null,
                    true
                ),
                new(
                    TacticalCombatControl.CameraRotateLeft,
                    TacticalCombatHudPanel.Frame,
                    new RectInt(497, 381, 42, 43),
                    "rotate-left",
                    "Rotate left",
                    100,
                    null,
                    true
                ),
                new(
                    TacticalCombatControl.CameraRotateRight,
                    TacticalCombatHudPanel.Frame,
                    new RectInt(582, 381, 43, 43),
                    "rotate-right",
                    "Rotate right",
                    100,
                    null,
                    true
                ),
                new(
                    TacticalCombatControl.CameraTiltUp,
                    TacticalCombatHudPanel.Frame,
                    new RectInt(539, 338, 43, 43),
                    "tilt-up",
                    "Tilt up",
                    100,
                    null,
                    true
                ),
                new(
                    TacticalCombatControl.CameraTiltDown,
                    TacticalCombatHudPanel.Frame,
                    new RectInt(540, 424, 43, 43),
                    "tilt-down",
                    "Tilt down",
                    100,
                    null,
                    true
                ),
                new(
                    TacticalCombatControl.SaveCamera,
                    TacticalCombatHudPanel.Frame,
                    new RectInt(594, 444, 43, 25),
                    "save-camera",
                    "Remember camera position",
                    0,
                    null,
                    true
                ),
                new(
                    TacticalCombatControl.RestoreCamera,
                    TacticalCombatHudPanel.Frame,
                    new RectInt(486, 444, 43, 25),
                    "restore-camera",
                    "Reset camera view",
                    0,
                    null,
                    true
                ),
                new(
                    TacticalCombatControl.FocusCamera,
                    TacticalCombatHudPanel.Frame,
                    new RectInt(549, 391, 23, 23),
                    "focus-camera",
                    "Reset camera subject",
                    0,
                    null,
                    true
                ),
                new(
                    TacticalCombatControl.PreviousObject,
                    TacticalCombatHudPanel.SingleObject,
                    new RectInt(7, 10, 11, 53),
                    "previous-object",
                    "Previous capital ship",
                    0,
                    null,
                    false
                ),
                new(
                    TacticalCombatControl.NextObject,
                    TacticalCombatHudPanel.SingleObject,
                    new RectInt(132, 10, 11, 53),
                    "next-object",
                    "Next capital ship",
                    0,
                    null,
                    false
                ),
                new(
                    TacticalCombatControl.PreviousObject,
                    TacticalCombatHudPanel.ObjectGroup,
                    new RectInt(7, 10, 11, 53),
                    "previous-object",
                    "Previous capital ship in task force",
                    0,
                    null,
                    false
                ),
                new(
                    TacticalCombatControl.NextObject,
                    TacticalCombatHudPanel.ObjectGroup,
                    new RectInt(132, 10, 11, 53),
                    "next-object",
                    "Next capital ship in task force",
                    0,
                    null,
                    false
                ),
                new(
                    TacticalCombatControl.OpenOrders,
                    TacticalCombatHudPanel.SingleObject,
                    new RectInt(80, 210, 58, 22),
                    "open-orders",
                    "Missions",
                    0,
                    null,
                    false
                ),
                new(
                    TacticalCombatControl.OpenTactics,
                    TacticalCombatHudPanel.SingleObject,
                    new RectInt(12, 210, 58, 22),
                    "open-tactics",
                    "Maneuvers and tactics",
                    0,
                    null,
                    false
                ),
                new(
                    TacticalCombatControl.OpenOrders,
                    TacticalCombatHudPanel.ObjectGroup,
                    new RectInt(80, 210, 58, 22),
                    "open-orders",
                    "Missions",
                    0,
                    null,
                    false
                ),
                new(
                    TacticalCombatControl.OpenTactics,
                    TacticalCombatHudPanel.ObjectGroup,
                    new RectInt(12, 210, 58, 22),
                    "open-tactics",
                    "Maneuvers and tactics",
                    0,
                    null,
                    false
                ),
                new(
                    TacticalCombatControl.OpenOrders,
                    TacticalCombatHudPanel.FighterGroup,
                    new RectInt(80, 210, 58, 22),
                    "open-orders",
                    "Missions",
                    0,
                    null,
                    false
                ),
                new(
                    TacticalCombatControl.OpenTactics,
                    TacticalCombatHudPanel.FighterGroup,
                    new RectInt(12, 210, 58, 22),
                    "open-tactics",
                    "Maneuvers and tactics",
                    0,
                    null,
                    false
                ),
                new(
                    TacticalCombatControl.AttackCapitalShips,
                    TacticalCombatHudPanel.Orders,
                    new RectInt(18, 129, 46, 26),
                    "attack-ships",
                    "Attack capital ships",
                    0,
                    null,
                    false
                ),
                new(
                    TacticalCombatControl.AttackFighters,
                    TacticalCombatHudPanel.Orders,
                    new RectInt(18, 159, 46, 26),
                    "attack-fighters",
                    "Attack fighters",
                    0,
                    null,
                    false
                ),
                new(
                    TacticalCombatControl.Recover,
                    TacticalCombatHudPanel.Orders,
                    new RectInt(86, 129, 46, 26),
                    "recover",
                    "Recover fighters",
                    0,
                    null,
                    false
                ),
                new(
                    TacticalCombatControl.AttackBattleStation,
                    TacticalCombatHudPanel.Orders,
                    new RectInt(86, 159, 46, 26),
                    "attack-station",
                    "Attack Death Star",
                    0,
                    null,
                    false
                ),
                new(
                    TacticalCombatControl.LeftHook,
                    TacticalCombatHudPanel.Tactics,
                    new RectInt(13, 159, 28, 28),
                    "left-hook",
                    "Left hook",
                    0,
                    null,
                    false
                ),
                new(
                    TacticalCombatControl.RightHook,
                    TacticalCombatHudPanel.Tactics,
                    new RectInt(43, 159, 28, 28),
                    "right-hook",
                    "Right hook",
                    0,
                    null,
                    false
                ),
                new(
                    TacticalCombatControl.Hammer,
                    TacticalCombatHudPanel.Tactics,
                    new RectInt(13, 129, 28, 28),
                    "hammer",
                    "Hammer",
                    0,
                    null,
                    false
                ),
                new(
                    TacticalCombatControl.Anvil,
                    TacticalCombatHudPanel.Tactics,
                    new RectInt(43, 129, 28, 28),
                    "anvil",
                    "Anvil",
                    0,
                    null,
                    false
                ),
                new(
                    TacticalCombatControl.Stop,
                    TacticalCombatHudPanel.Tactics,
                    new RectInt(75, 144, 28, 28),
                    "stop",
                    "Stop",
                    0,
                    null,
                    false
                ),
                new(
                    TacticalCombatControl.CloseAttack,
                    TacticalCombatHudPanel.Tactics,
                    new RectInt(111, 132, 24, 52),
                    "attack-mode",
                    "Stand off / surround",
                    0,
                    null,
                    false
                ),
                new(
                    TacticalCombatControl.ConfirmPanel,
                    TacticalCombatHudPanel.Orders,
                    new RectInt(66, 202, 27, 25),
                    "confirm",
                    "Confirm",
                    0,
                    null,
                    false
                ),
                new(
                    TacticalCombatControl.CancelPanel,
                    TacticalCombatHudPanel.Orders,
                    new RectInt(106, 202, 27, 25),
                    "cancel",
                    "Cancel",
                    0,
                    null,
                    false
                ),
                new(
                    TacticalCombatControl.ConfirmPanel,
                    TacticalCombatHudPanel.Tactics,
                    new RectInt(66, 202, 27, 25),
                    "confirm",
                    "Confirm",
                    0,
                    null,
                    false
                ),
                new(
                    TacticalCombatControl.CancelPanel,
                    TacticalCombatHudPanel.Tactics,
                    new RectInt(106, 202, 27, 25),
                    "cancel",
                    "Cancel",
                    0,
                    null,
                    false
                ),
                new(
                    TacticalCombatControl.ConfirmWithdrawal,
                    TacticalCombatHudPanel.WithdrawalConfirmation,
                    new RectInt(66, 202, 27, 25),
                    "confirm",
                    "Confirm withdraw",
                    0,
                    null,
                    false
                ),
                new(
                    TacticalCombatControl.CancelWithdrawal,
                    TacticalCombatHudPanel.WithdrawalConfirmation,
                    new RectInt(106, 202, 27, 25),
                    "cancel",
                    "Cancel",
                    0,
                    null,
                    false
                ),
                new(
                    TacticalCombatControl.Withdraw,
                    TacticalCombatHudPanel.Commands,
                    new RectInt(18, 19, 48, 34),
                    "withdraw",
                    "Withdraw from battle",
                    0,
                    null,
                    false
                ),
                new(
                    TacticalCombatControl.ResolveImmediately,
                    TacticalCombatHudPanel.Commands,
                    new RectInt(14, 93, 44, 25),
                    "immediate-result",
                    "Get immediate result",
                    0,
                    null,
                    false
                ),
                new(
                    TacticalCombatControl.ToggleAutomation,
                    TacticalCombatHudPanel.Commands,
                    new RectInt(87, 93, 45, 25),
                    "automated-control",
                    "Observe battle / take command",
                    0,
                    null,
                    false
                ),
                new(
                    TacticalCombatControl.OpenOptions,
                    TacticalCombatHudPanel.Commands,
                    new RectInt(23, 184, 52, 33),
                    "options",
                    "Game options",
                    0,
                    null,
                    false
                ),
                new(
                    TacticalCombatControl.CloseMenu,
                    TacticalCombatHudPanel.Commands,
                    new RectInt(97, 184, 29, 33),
                    "close-menu",
                    "Close",
                    0,
                    null,
                    false
                ),
                new(
                    TacticalCombatControl.Superlaser,
                    TacticalCombatHudPanel.Frame,
                    new RectInt(611, 2, 21, 20),
                    "superlaser",
                    "Target Death Star superlaser",
                    0,
                    null,
                    false
                ),
            }
        );
}
