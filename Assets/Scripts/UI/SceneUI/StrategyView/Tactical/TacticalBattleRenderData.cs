using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Identifies whether a ship system is working, knocked out, or not fitted.
/// </summary>
internal enum TacticalSystemState
{
    Online,
    Offline,
    Absent,
}

/// <summary>
/// Describes one unit sprite, its status bars, and its selection state in source space.
/// </summary>
internal sealed class TacticalUnitRenderData
{
    public float X { get; }

    public float Y { get; }

    public float Width { get; }

    public float Height { get; }

    public float RotationDegrees { get; }

    public Texture2D Texture { get; }

    public float HullFraction { get; }

    public float ShieldFraction { get; }

    public bool ShowShields { get; }

    public bool Selected { get; }

    public Color BarColor { get; }

    public float Alpha { get; }

    /// <summary>
    /// Creates presentation data for one tactical unit.
    /// </summary>
    /// <param name="x">The source-space horizontal center.</param>
    /// <param name="y">The source-space vertical center.</param>
    /// <param name="width">The source-space sprite width.</param>
    /// <param name="height">The source-space sprite height.</param>
    /// <param name="rotationDegrees">The counter-clockwise sprite rotation.</param>
    /// <param name="texture">The unit sprite texture.</param>
    /// <param name="hullFraction">The remaining hull or squadron fraction.</param>
    /// <param name="shieldFraction">The remaining shield fraction.</param>
    /// <param name="showShields">Whether the shield bar is displayed.</param>
    /// <param name="selected">Whether the unit is selected.</param>
    /// <param name="barColor">The hull bar color identifying the unit's side.</param>
    /// <param name="alpha">The sprite opacity.</param>
    public TacticalUnitRenderData(
        float x,
        float y,
        float width,
        float height,
        float rotationDegrees,
        Texture2D texture,
        float hullFraction,
        float shieldFraction,
        bool showShields,
        bool selected,
        Color barColor,
        float alpha
    )
    {
        X = x;
        Y = y;
        Width = width;
        Height = height;
        RotationDegrees = rotationDegrees;
        Texture = texture;
        HullFraction = hullFraction;
        ShieldFraction = shieldFraction;
        ShowShields = showShields;
        Selected = selected;
        BarColor = barColor;
        Alpha = alpha;
    }
}

/// <summary>
/// Describes one weapon-fire line in source space.
/// </summary>
internal sealed class TacticalShotRenderData
{
    public float X { get; }

    public float Y { get; }

    public float Length { get; }

    public float Thickness { get; }

    public float RotationDegrees { get; }

    public Color Color { get; }

    /// <summary>
    /// Creates presentation data for one shot.
    /// </summary>
    /// <param name="x">The source-space horizontal center.</param>
    /// <param name="y">The source-space vertical center.</param>
    /// <param name="length">The source-space line length.</param>
    /// <param name="thickness">The source-space line thickness.</param>
    /// <param name="rotationDegrees">The counter-clockwise line rotation.</param>
    /// <param name="color">The line color.</param>
    public TacticalShotRenderData(
        float x,
        float y,
        float length,
        float thickness,
        float rotationDegrees,
        Color color
    )
    {
        X = x;
        Y = y;
        Length = length;
        Thickness = thickness;
        RotationDegrees = rotationDegrees;
        Color = color;
    }
}

/// <summary>
/// Describes one ship system indicator in the information panel.
/// </summary>
internal sealed class TacticalSystemRenderData
{
    public string Label { get; }

    public TacticalSystemState State { get; }

    /// <summary>
    /// Creates one system indicator.
    /// </summary>
    /// <param name="label">The short system name.</param>
    /// <param name="state">The system's condition.</param>
    public TacticalSystemRenderData(string label, TacticalSystemState state)
    {
        Label = label ?? string.Empty;
        State = state;
    }
}

/// <summary>
/// Describes one crew member or unit carried aboard the inspected ship.
/// </summary>
internal sealed class TacticalPersonnelRenderData
{
    public Texture2D Portrait { get; }

    public string Name { get; }

    /// <summary>
    /// Creates one personnel entry.
    /// </summary>
    /// <param name="portrait">The portrait texture, or null when unavailable.</param>
    /// <param name="name">The display name.</param>
    public TacticalPersonnelRenderData(Texture2D portrait, string name)
    {
        Portrait = portrait;
        Name = name ?? string.Empty;
    }
}

/// <summary>
/// Describes the information panel for the inspected unit.
/// </summary>
internal sealed class TacticalPanelRenderData
{
    public string Name { get; }

    public string Subtitle { get; }

    public Texture2D Portrait { get; }

    public bool ShowShields { get; }

    public float ShieldFraction { get; }

    public string ShieldText { get; }

    public float HullFraction { get; }

    public string HullText { get; }

    public IReadOnlyList<TacticalSystemRenderData> Systems { get; }

    public IReadOnlyList<float> ArcCharges { get; }

    public IReadOnlyList<TacticalPersonnelRenderData> Personnel { get; }

    public string PersonnelText { get; }

    public string OrdersText { get; }

    public string TacticsText { get; }

    public string TargetPriorityLabel { get; }

    public bool TargetPriorityInteractable { get; }

    /// <summary>
    /// Creates the information panel presentation for one unit.
    /// </summary>
    /// <param name="name">The unit name.</param>
    /// <param name="subtitle">The fleet or squadron description.</param>
    /// <param name="portrait">The unit portrait, or null when unavailable.</param>
    /// <param name="showShields">Whether the shield bar applies to this unit.</param>
    /// <param name="shieldFraction">The remaining shield fraction.</param>
    /// <param name="shieldText">The shield readout.</param>
    /// <param name="hullFraction">The remaining hull or squadron fraction.</param>
    /// <param name="hullText">The hull or squadron readout.</param>
    /// <param name="systems">The ship system indicators.</param>
    /// <param name="arcCharges">The fore, aft, port, and starboard weapon-arc charge fractions.</param>
    /// <param name="personnel">The personnel aboard, in display order.</param>
    /// <param name="personnelText">The personnel summary.</param>
    /// <param name="ordersText">The current orders.</param>
    /// <param name="tacticsText">The control mode description.</param>
    /// <param name="targetPriorityLabel">Which enemy class the unit engages first.</param>
    /// <param name="targetPriorityInteractable">Whether the player may change that priority.</param>
    public TacticalPanelRenderData(
        string name,
        string subtitle,
        Texture2D portrait,
        bool showShields,
        float shieldFraction,
        string shieldText,
        float hullFraction,
        string hullText,
        IReadOnlyList<TacticalSystemRenderData> systems,
        IReadOnlyList<float> arcCharges,
        IReadOnlyList<TacticalPersonnelRenderData> personnel,
        string personnelText,
        string ordersText,
        string tacticsText,
        string targetPriorityLabel,
        bool targetPriorityInteractable
    )
    {
        Name = name ?? string.Empty;
        Subtitle = subtitle ?? string.Empty;
        Portrait = portrait;
        ShowShields = showShields;
        ShieldFraction = shieldFraction;
        ShieldText = shieldText ?? string.Empty;
        HullFraction = hullFraction;
        HullText = hullText ?? string.Empty;
        Systems = systems ?? System.Array.Empty<TacticalSystemRenderData>();
        ArcCharges = arcCharges ?? System.Array.Empty<float>();
        Personnel = personnel ?? System.Array.Empty<TacticalPersonnelRenderData>();
        PersonnelText = personnelText ?? string.Empty;
        OrdersText = ordersText ?? string.Empty;
        TacticsText = tacticsText ?? string.Empty;
        TargetPriorityLabel = targetPriorityLabel ?? string.Empty;
        TargetPriorityInteractable = targetPriorityInteractable;
    }
}

/// <summary>
/// Describes the superlaser charge bar shown at the top of the information panel while the
/// player commands a battle station.
/// </summary>
internal sealed class TacticalSuperlaserRenderData
{
    public string Label { get; }

    public float ChargeFraction { get; }

    public bool Ready { get; }

    public bool Armed { get; }

    /// <summary>
    /// Creates the superlaser bar presentation.
    /// </summary>
    /// <param name="label">The caption drawn over the bar.</param>
    /// <param name="chargeFraction">How charged the superlaser is, from 0 to 1.</param>
    /// <param name="ready">Whether the player can arm the superlaser now.</param>
    /// <param name="armed">Whether the player is choosing a target.</param>
    public TacticalSuperlaserRenderData(string label, float chargeFraction, bool ready, bool armed)
    {
        Label = label ?? string.Empty;
        ChargeFraction = chargeFraction;
        Ready = ready;
        Armed = armed;
    }
}

/// <summary>
/// Describes the complete tactical battle presentation for one frame.
/// </summary>
internal sealed class TacticalBattleRenderData
{
    public string Title { get; }

    public string AttackerStatus { get; }

    public string DefenderStatus { get; }

    public string ClockStatus { get; }

    public string Hint { get; }

    public TacticalPanelRenderData Panel { get; }

    public string RetreatLabel { get; }

    public bool RetreatInteractable { get; }

    public string AutoLabel { get; }

    public bool AutoInteractable { get; }

    public string PauseLabel { get; }

    public string SpeedLabel { get; }

    public Texture2D StarfieldTexture { get; }

    public Texture2D PlanetTexture { get; }

    public RectInt PlanetBounds { get; }

    public IReadOnlyList<TacticalUnitRenderData> Units { get; }

    public IReadOnlyList<TacticalShotRenderData> Shots { get; }

    public RectInt? SelectionBox { get; }

    public TacticalRosterRenderData Roster { get; }

    /// <summary>
    /// Gets the superlaser charge bar, or null when the player commands no battle station.
    /// </summary>
    public TacticalSuperlaserRenderData Superlaser { get; }

    public string Status => $"{AttackerStatus}   vs   {DefenderStatus}   |   {ClockStatus}";

    /// <summary>
    /// Creates presentation data for one tactical battle frame.
    /// </summary>
    /// <param name="title">The battle title.</param>
    /// <param name="attackerStatus">The attacking force's status line.</param>
    /// <param name="defenderStatus">The defending force's status line.</param>
    /// <param name="clockStatus">The elapsed time and pause state.</param>
    /// <param name="hint">The control hint or inspection prompt.</param>
    /// <param name="panel">The inspected unit's panel, or null when nothing is inspected.</param>
    /// <param name="retreatLabel">The retreat button caption.</param>
    /// <param name="retreatInteractable">Whether the retreat button accepts input.</param>
    /// <param name="autoLabel">The automatic-control button caption.</param>
    /// <param name="autoInteractable">Whether the automatic-control button accepts input.</param>
    /// <param name="pauseLabel">The pause button caption.</param>
    /// <param name="speedLabel">The speed button caption.</param>
    /// <param name="starfieldTexture">The background starfield texture.</param>
    /// <param name="planetTexture">The planet backdrop texture.</param>
    /// <param name="planetBounds">The source-space planet backdrop bounds.</param>
    /// <param name="units">The unit presentations.</param>
    /// <param name="shots">The weapon-fire presentations.</param>
    /// <param name="selectionBox">The source-space drag-selection bounds, when dragging.</param>
    /// <param name="roster">The roster bar, or null to hide it.</param>
    /// <param name="superlaser">The superlaser charge bar, or null to hide it.</param>
    public TacticalBattleRenderData(
        string title,
        string attackerStatus,
        string defenderStatus,
        string clockStatus,
        string hint,
        TacticalPanelRenderData panel,
        string retreatLabel,
        bool retreatInteractable,
        string autoLabel,
        bool autoInteractable,
        string pauseLabel,
        string speedLabel,
        Texture2D starfieldTexture,
        Texture2D planetTexture,
        RectInt planetBounds,
        IReadOnlyList<TacticalUnitRenderData> units,
        IReadOnlyList<TacticalShotRenderData> shots,
        RectInt? selectionBox,
        TacticalRosterRenderData roster,
        TacticalSuperlaserRenderData superlaser = null
    )
    {
        Title = title ?? string.Empty;
        AttackerStatus = attackerStatus ?? string.Empty;
        DefenderStatus = defenderStatus ?? string.Empty;
        ClockStatus = clockStatus ?? string.Empty;
        Hint = hint ?? string.Empty;
        Panel = panel;
        RetreatLabel = retreatLabel ?? string.Empty;
        RetreatInteractable = retreatInteractable;
        AutoLabel = autoLabel ?? string.Empty;
        AutoInteractable = autoInteractable;
        PauseLabel = pauseLabel ?? string.Empty;
        SpeedLabel = speedLabel ?? string.Empty;
        StarfieldTexture = starfieldTexture;
        PlanetTexture = planetTexture;
        PlanetBounds = planetBounds;
        Units = units ?? System.Array.Empty<TacticalUnitRenderData>();
        Shots = shots ?? System.Array.Empty<TacticalShotRenderData>();
        SelectionBox = selectionBox;
        Roster = roster;
        Superlaser = superlaser;
    }
}

/// <summary>
/// One entry in the roster bar: a unit portrait, its label, and its state.
/// </summary>
public sealed class TacticalRosterSlotRenderData
{
    /// <summary>
    /// Creates a roster entry.
    /// </summary>
    /// <param name="label">The short label, such as a ship number or squadron size.</param>
    /// <param name="portrait">The unit portrait, or null.</param>
    /// <param name="highlighted">Whether the unit is inspected or selected.</param>
    /// <param name="alive">Whether the unit is still in the battle.</param>
    public TacticalRosterSlotRenderData(
        string label,
        Texture2D portrait,
        bool highlighted,
        bool alive
    )
    {
        Label = label ?? string.Empty;
        Portrait = portrait;
        Highlighted = highlighted;
        Alive = alive;
    }

    public string Label { get; }

    public Texture2D Portrait { get; }

    public bool Highlighted { get; }

    public bool Alive { get; }
}

/// <summary>
/// The roster bar across the top of the arena: one side's capital ships, then every one of its
/// squadrons grouped by fighter type.
/// </summary>
public sealed class TacticalRosterRenderData
{
    /// <summary>
    /// Creates the roster presentation.
    /// </summary>
    /// <param name="sideLabel">The listed side's name, shown on the side toggle.</param>
    /// <param name="ships">The side's capital ships in roster order.</param>
    /// <param name="squadronGroups">The side's squadrons, one group per fighter type.</param>
    public TacticalRosterRenderData(
        string sideLabel,
        IReadOnlyList<TacticalRosterSlotRenderData> ships,
        IReadOnlyList<IReadOnlyList<TacticalRosterSlotRenderData>> squadronGroups
    )
    {
        SideLabel = sideLabel ?? string.Empty;
        Ships = ships ?? System.Array.Empty<TacticalRosterSlotRenderData>();
        SquadronGroups =
            squadronGroups ?? System.Array.Empty<IReadOnlyList<TacticalRosterSlotRenderData>>();
    }

    public string SideLabel { get; }

    public IReadOnlyList<TacticalRosterSlotRenderData> Ships { get; }

    /// <summary>
    /// Gets the squadrons grouped by fighter type in first-seen order; slot indexes count across
    /// the groups, matching the side's squadron list in group order.
    /// </summary>
    public IReadOnlyList<IReadOnlyList<TacticalRosterSlotRenderData>> SquadronGroups { get; }
}
