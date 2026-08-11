using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Presentation-only description of one key binding for the Controls page.
/// </summary>
public sealed class OptionsBindingRow
{
    /// <summary>
    /// Creates an immutable binding row.
    /// </summary>
    /// <param name="action">The human-readable action name.</param>
    /// <param name="primary">The primary bound keys.</param>
    /// <param name="secondary">The secondary bound keys, if any.</param>
    /// <param name="isHeader">Whether this row is a group header rather than a bindable action.</param>
    public OptionsBindingRow(string action, string primary, string secondary = "", bool isHeader = false)
    {
        Action = action ?? string.Empty;
        Primary = primary ?? string.Empty;
        Secondary = secondary ?? string.Empty;
        IsHeader = isHeader;
    }

    /// <summary>Gets the human-readable action name.</summary>
    public string Action { get; }

    /// <summary>Gets the primary bound keys.</summary>
    public string Primary { get; }

    /// <summary>Gets the secondary bound keys.</summary>
    public string Secondary { get; }

    /// <summary>Gets whether this row is a group header rather than a bindable action.</summary>
    public bool IsHeader { get; }
}

/// <summary>
/// Presentation-only description of one Save/Load row: either the "Create New Save" action or one
/// existing save (faction icon, display name, and save date).
/// </summary>
public sealed class OptionsSaveSlot
{
    /// <summary>
    /// Creates an immutable save row.
    /// </summary>
    /// <param name="name">The row display name.</param>
    /// <param name="date">The save date line (empty for the create-new row).</param>
    /// <param name="factionIcon">The saved faction's icon, or null.</param>
    /// <param name="isCreateNew">Whether this row creates a new save.</param>
    /// <param name="fileName">The save file name, or null for the create-new row.</param>
    public OptionsSaveSlot(
        string name,
        string date,
        Texture2D factionIcon,
        bool isCreateNew,
        string fileName
    )
    {
        Name = name ?? string.Empty;
        Date = date ?? string.Empty;
        FactionIcon = factionIcon;
        IsCreateNew = isCreateNew;
        FileName = fileName ?? string.Empty;
    }

    /// <summary>Gets the row display name.</summary>
    public string Name { get; }

    /// <summary>Gets the save date line.</summary>
    public string Date { get; }

    /// <summary>Gets the saved faction's icon, or null.</summary>
    public Texture2D FactionIcon { get; }

    /// <summary>Gets whether this row creates a new save.</summary>
    public bool IsCreateNew { get; }

    /// <summary>Gets the save file name backing this row.</summary>
    public string FileName { get; }
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
    /// <param name="canReturnToMainMenu">Whether returning to the Main Menu is possible.</param>
    /// <param name="listeningRow">The binding row awaiting a key press, or -1.</param>
    /// <param name="listeningSecondary">Whether the secondary column is awaiting a key press.</param>
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
        bool canReturnToGame,
        bool canReturnToMainMenu,
        int listeningRow,
        bool listeningSecondary
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
        CanReturnToMainMenu = canReturnToMainMenu;
        ListeningRow = listeningRow;
        ListeningSecondary = listeningSecondary;
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

    /// <summary>Gets whether returning to the Main Menu is possible (false when already there).</summary>
    public bool CanReturnToMainMenu { get; }

    /// <summary>Gets the binding row awaiting a key press, or -1.</summary>
    public int ListeningRow { get; }

    /// <summary>Gets whether the secondary column is awaiting a key press.</summary>
    public bool ListeningSecondary { get; }
}
