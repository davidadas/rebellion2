using System;
using System.Globalization;
using Rebellion.Gameplay;

/// <summary>
/// Formats the selected participant's requested orders, applied group routing, and system condition.
/// </summary>
public sealed class TacticalCombatInspection
{
    private readonly TacticalCombatSession _session;

    /// <summary>
    /// Retains the encounter that owns group membership and live movement support.
    /// </summary>
    /// <param name="session">The encounter whose right panel is being displayed.</param>
    public TacticalCombatInspection(TacticalCombatSession session)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
    }

    /// <summary>
    /// Reads one participant without substituting applied ownership for its requested assignment.
    /// </summary>
    /// <param name="unit">The inspected participant.</param>
    /// <returns>The current participant details and system images.</returns>
    public TacticalCombatInspectionData ReadUnit(TacticalCombatUnit unit)
    {
        TacticalCombatGroup requested = _session.GetRequestedGroup(unit);
        int tracked = 0;
        for (int slot = 0; slot < 8; slot++)
            if (
                requested != null
                && ReferenceEquals(_session.GetObjectGroup(_session.PlayerSide, slot), requested)
            )
            {
                tracked = slot + 1;
                break;
            }
        string assignment =
            tracked == 0
                ? "Task Force Unassigned"
                : "Task Force # " + tracked.ToString(CultureInfo.InvariantCulture);
        bool friendly = unit.Side == _session.PlayerSide;
        string orders = "Orders Unavailable";
        string tactics = null;
        if (friendly)
        {
            orders = DescribeOrder(requested?.Order ?? TacticalCombatOrder.None, false);
            if (requested?.Order == TacticalCombatOrder.Withdraw && IsDisabled(unit))
                orders = "Disabled";
            TacticalCombatGroup applied = _session.GetGroup(unit);
            if (applied?.Kind == TacticalCombatGroupKind.Command)
            {
                if (applied.RequestedWaypoints.Count != 0)
                    orders = IsDisabled(unit) ? "Disabled" : "Navigating";
                else if (applied.RequestedTargets.Count != 0)
                {
                    if (applied.Order != TacticalCombatOrder.HoldFire)
                        orders = "Attacking";
                    orders += " " + applied.RequestedTargets[0].DisplayName;
                }
            }
            tactics = DescribeTactics(requested?.RequestedFormation);
        }
        TacticalCombatSystems systems = unit.Systems;
        int hyperdrive = Level(systems.GetHyperdriveRatio());
        int movement = Level(_session.GetMovementRatio(unit));
        int tractor = Level(systems.GetTractorRatio());
        int weapons = Level(systems.GetWeaponRechargeRatio());
        int shields = Level(systems.GetShieldRechargeRatio());
        return new TacticalCombatInspectionData(
            unit.DisplayName,
            assignment,
            orders,
            tactics,
            null,
            unit.Definition.PresentationIndex,
            shields,
            weapons,
            tractor,
            movement,
            hyperdrive
        );
    }

    /// <summary>
    /// Reads the category's requested type count and its applied surviving and complete fighter totals.
    /// </summary>
    /// <param name="group">The inspected fighter category.</param>
    /// <param name="category">The fixed zero-based category.</param>
    /// <param name="factionIndex">The player's authored faction index.</param>
    /// <returns>The category's details, retaining absent preview and count updates as null.</returns>
    public TacticalCombatInspectionData ReadFighters(
        TacticalCombatGroup group,
        int category,
        int factionIndex
    )
    {
        string title = category switch
        {
            0 => "Red Group",
            1 => "Blue Group",
            2 => "Green Group",
            3 => "Gold Group",
            _ => string.Empty,
        };
        string orders = DescribeOrder(group.Order, true);
        if (group.RequestedWaypoints.Count != 0)
            orders = "Navigating";
        else if (group.RequestedTargets.Count != 0)
        {
            if (group.Order != TacticalCombatOrder.HoldFire)
                orders = "Attacking";
            orders += " " + group.RequestedTargets[0].DisplayName;
        }
        int first = 29 + factionIndex * 4;
        int selected = first + 4;
        foreach (TacticalCombatUnit member in group.RequestedMembers)
            if (
                member.Definition.PresentationIndex >= first
                && member.Definition.PresentationIndex < selected
            )
                selected = member.Definition.PresentationIndex;
        string counts = null;
        if (selected < first + 4)
        {
            uint matching = 0;
            uint current = 0;
            uint maximum = 0;
            foreach (TacticalCombatUnit member in group.RequestedMembers)
                if (member.Definition.PresentationIndex == selected)
                    matching++;
            foreach (TacticalCombatUnit member in group.Members)
            {
                TacticalCombatSystems systems = member.Systems;
                float scale = systems.MaximumHull / 8f;
                maximum = unchecked(
                    maximum + Math.Max(1u, Truncate((double)systems.MaximumHull / 8f))
                );
                current = unchecked(
                    current
                    + Math.Max(1u, Truncate((double)systems.Hull * scale / systems.MaximumHull))
                );
            }
            counts = string.Concat(
                FighterLabel(selected),
                " ",
                matching.ToString(CultureInfo.InvariantCulture),
                " ",
                current.ToString(CultureInfo.InvariantCulture),
                "/",
                maximum.ToString(CultureInfo.InvariantCulture)
            );
        }
        return new TacticalCombatInspectionData(
            title,
            null,
            orders,
            DescribeTactics(group.RequestedFormation),
            counts,
            selected < first + 4 ? selected : null,
            0,
            0,
            0,
            0,
            0
        );
    }

    /// <summary>
    /// Selects the status image from the unsigned truncated percentage without adding an upper clamp.
    /// </summary>
    /// <param name="ratio">The live system budget divided by its stored base.</param>
    /// <returns>The zero-based image in the five-state status sequence.</returns>
    public static int Level(double ratio)
    {
        uint percent = Truncate(ratio * 100);
        return percent > 74 ? 4
            : percent > 49 ? 3
            : percent > 24 ? 2
            : percent != 0 ? 1
            : 0;
    }

    /// <summary>
    /// Tests movement before the status text's single-precision comparison.
    /// </summary>
    /// <param name="unit">The inspected participant.</param>
    /// <returns>Whether the live movement is zero or unordered after its consumer's store.</returns>
    private bool IsDisabled(TacticalCombatUnit unit)
    {
        float movement = (float)_session.GetMovementBudget(unit);
        return movement == 0 || float.IsNaN(movement);
    }

    /// <summary>
    /// Resolves the requested order's authored display label.
    /// </summary>
    /// <param name="order">The requested group order.</param>
    /// <param name="fighter">Whether the fighter panel owns the recover label.</param>
    /// <returns>The corresponding order text.</returns>
    private static string DescribeOrder(TacticalCombatOrder order, bool fighter) =>
        order switch
        {
            TacticalCombatOrder.HoldFire => "Escort",
            TacticalCombatOrder.Recover => fighter ? "Recover" : string.Empty,
            TacticalCombatOrder.Withdraw => "Withdraw",
            TacticalCombatOrder.AttackFighters => "Attack Fighters",
            TacticalCombatOrder.AttackCapitalShips => "Attack Capital Ships",
            TacticalCombatOrder.AttackBattleStation => "Attack Death Star",
            TacticalCombatOrder.LeftHook => "Left Hook",
            TacticalCombatOrder.RightHook => "Right Hook",
            TacticalCombatOrder.Hammer => "Hammer",
            TacticalCombatOrder.Anvil => "Anvil",
            _ => "No Orders",
        };

    /// <summary>
    /// Formats the requested formation selector's tactical description.
    /// </summary>
    /// <param name="formation">The requested formation, when an owner exists.</param>
    /// <returns>The authored tactics line.</returns>
    private static string DescribeTactics(TacticalCombatFormationKind? formation) =>
        "Tactics: "
        + (formation == TacticalCombatFormationKind.Surround ? "Surround" : "Stand Off");

    /// <summary>
    /// Resolves the chosen fighter type's authored count label.
    /// </summary>
    /// <param name="index">The authored presentation index from 29 through 36.</param>
    /// <returns>The type-specific count prefix.</returns>
    private static string FighterLabel(int index) =>
        index switch
        {
            29 => "X-wings:",
            30 => "Y-wings:",
            31 => "A-wings:",
            32 => "B-wings:",
            33 => "Fighters:",
            34 => "Intrcpts:",
            35 => "Bombers:",
            36 => "Defenders:",
            _ => string.Empty,
        };

    /// <summary>
    /// Converts a truncated signed 64-bit result to its low unsigned word, including the invalid result.
    /// </summary>
    /// <param name="value">The retained arithmetic result.</param>
    /// <returns>The low 32 bits, or zero for the indefinite signed 64-bit conversion.</returns>
    private static uint Truncate(double value) =>
        double.IsNaN(value) || value < long.MinValue || value >= 9223372036854775808d
            ? 0
            : unchecked((uint)(long)value);
}

/// <summary>
/// Carries one inspection update; null detail text leaves that panel's retained text untouched.
/// </summary>
public readonly struct TacticalCombatInspectionData
{
    public string Name { get; }
    public string Assignment { get; }
    public string Orders { get; }
    public string Tactics { get; }
    public string FighterCounts { get; }
    public int? PreviewIndex { get; }
    public int Shields { get; }
    public int Weapons { get; }
    public int Tractor { get; }
    public int Movement { get; }
    public int Hyperdrive { get; }

    /// <summary>
    /// Captures formatted detail rows, optional preview changes, and five system image levels.
    /// </summary>
    /// <param name="name">The unit or category label.</param>
    /// <param name="assignment">The tracked task-force label, when updated.</param>
    /// <param name="orders">The order and optional first target label.</param>
    /// <param name="tactics">The tactics row, when updated.</param>
    /// <param name="fighterCounts">The fighter type and counts, when updated.</param>
    /// <param name="previewIndex">The addressed type image, when updated.</param>
    /// <param name="shields">The shield-recharge image level.</param>
    /// <param name="weapons">The weapon-recharge image level.</param>
    /// <param name="tractor">The tractor-power image level.</param>
    /// <param name="movement">The engine image level.</param>
    /// <param name="hyperdrive">The hyperdrive image level.</param>
    public TacticalCombatInspectionData(
        string name,
        string assignment,
        string orders,
        string tactics,
        string fighterCounts,
        int? previewIndex,
        int shields,
        int weapons,
        int tractor,
        int movement,
        int hyperdrive
    )
    {
        Name = name;
        Assignment = assignment;
        Orders = orders;
        Tactics = tactics;
        FighterCounts = fighterCounts;
        PreviewIndex = previewIndex;
        Shields = shields;
        Weapons = weapons;
        Tractor = tractor;
        Movement = movement;
        Hyperdrive = hyperdrive;
    }
}
