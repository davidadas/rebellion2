using System;
using System.Collections.Generic;

/// <summary>
/// Presentation-only description of one key binding for the Controls page.
/// </summary>
public sealed class OptionsBindingRow
{
    /// <summary>
    /// Creates an immutable binding row.
    /// </summary>
    /// <param name="action">The human-readable action name.</param>
    /// <param name="keys">The human-readable bound keys.</param>
    public OptionsBindingRow(string action, string keys)
    {
        Action = action ?? string.Empty;
        Keys = keys ?? string.Empty;
    }

    /// <summary>Gets the human-readable action name.</summary>
    public string Action { get; }

    /// <summary>Gets the human-readable bound keys.</summary>
    public string Keys { get; }
}

/// <summary>
/// Presentation-only description of one save slot for the Save/Load page.
/// </summary>
public sealed class OptionsSaveSlot
{
    /// <summary>
    /// Creates an immutable save-slot row.
    /// </summary>
    /// <param name="name">The slot display name.</param>
    /// <param name="detail">The secondary detail line.</param>
    /// <param name="filled">Whether the slot holds a save.</param>
    public OptionsSaveSlot(string name, string detail, bool filled)
    {
        Name = name ?? string.Empty;
        Detail = detail ?? string.Empty;
        Filled = filled;
    }

    /// <summary>Gets the slot display name.</summary>
    public string Name { get; }

    /// <summary>Gets the secondary detail line.</summary>
    public string Detail { get; }

    /// <summary>Gets whether the slot holds a save.</summary>
    public bool Filled { get; }
}

/// <summary>
/// Immutable snapshot the Options overlay renders each frame.
/// </summary>
public sealed class OptionsMenuRenderData
{
    /// <summary>
    /// Creates an immutable Options presentation snapshot.
    /// </summary>
    /// <param name="x">The source-space horizontal window position.</param>
    /// <param name="y">The source-space vertical window position.</param>
    /// <param name="activeTab">The currently visible page.</param>
    /// <param name="resolutionLabel">The current resolution value text.</param>
    /// <param name="fullScreenLabel">The current display-mode value text.</param>
    /// <param name="tacticalStates">The current detail-toggle states keyed by option.</param>
    /// <param name="volumes">The five normalized volume values in channel order.</param>
    /// <param name="bindings">The read-only key-binding rows.</param>
    /// <param name="saveSlots">The save-slot rows.</param>
    /// <param name="selectedSlot">The selected save-slot index, or -1.</param>
    /// <param name="canSave">Whether saving is currently available.</param>
    /// <param name="canLoad">Whether loading is currently available.</param>
    /// <param name="canReturnToGame">Whether a running game exists to return to.</param>
    public OptionsMenuRenderData(
        int x,
        int y,
        OptionsMenuTab activeTab,
        string resolutionLabel,
        string fullScreenLabel,
        IReadOnlyDictionary<UserTacticalOption, bool> tacticalStates,
        IReadOnlyList<float> volumes,
        IReadOnlyList<OptionsBindingRow> bindings,
        IReadOnlyList<OptionsSaveSlot> saveSlots,
        int selectedSlot,
        bool canSave,
        bool canLoad,
        bool canReturnToGame
    )
    {
        X = x;
        Y = y;
        ActiveTab = activeTab;
        ResolutionLabel = resolutionLabel ?? string.Empty;
        FullScreenLabel = fullScreenLabel ?? string.Empty;
        TacticalStates = tacticalStates ?? new Dictionary<UserTacticalOption, bool>();
        Volumes = volumes ?? Array.Empty<float>();
        Bindings = bindings ?? Array.Empty<OptionsBindingRow>();
        SaveSlots = saveSlots ?? Array.Empty<OptionsSaveSlot>();
        SelectedSlot = selectedSlot;
        CanSave = canSave;
        CanLoad = canLoad;
        CanReturnToGame = canReturnToGame;
    }

    /// <summary>Gets the source-space horizontal window position.</summary>
    public int X { get; }

    /// <summary>Gets the source-space vertical window position.</summary>
    public int Y { get; }

    /// <summary>Gets the currently visible page.</summary>
    public OptionsMenuTab ActiveTab { get; }

    /// <summary>Gets the current resolution value text.</summary>
    public string ResolutionLabel { get; }

    /// <summary>Gets the current display-mode value text.</summary>
    public string FullScreenLabel { get; }

    /// <summary>Gets the current detail-toggle states keyed by option.</summary>
    public IReadOnlyDictionary<UserTacticalOption, bool> TacticalStates { get; }

    /// <summary>Gets the five normalized volume values in channel order.</summary>
    public IReadOnlyList<float> Volumes { get; }

    /// <summary>Gets the read-only key-binding rows.</summary>
    public IReadOnlyList<OptionsBindingRow> Bindings { get; }

    /// <summary>Gets the save-slot rows.</summary>
    public IReadOnlyList<OptionsSaveSlot> SaveSlots { get; }

    /// <summary>Gets the selected save-slot index, or -1.</summary>
    public int SelectedSlot { get; }

    /// <summary>Gets whether saving is currently available.</summary>
    public bool CanSave { get; }

    /// <summary>Gets whether loading is currently available.</summary>
    public bool CanLoad { get; }

    /// <summary>Gets whether a running game exists to return to.</summary>
    public bool CanReturnToGame { get; }
}
