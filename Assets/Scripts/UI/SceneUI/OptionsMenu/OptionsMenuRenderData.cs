using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Identifies the active Options menu page.
/// </summary>
public enum OptionsMenuTab
{
    Graphics,
    Audio,
    SaveLoad,
    Controls,
}

/// <summary>
/// Represents one binding row in the Controls menu.
/// </summary>
public sealed class OptionsBindingRow
{
    /// <summary>
    /// Creates a binding row.
    /// </summary>
    /// <param name="action">The human-readable action name.</param>
    /// <param name="primary">The primary bound keys.</param>
    /// <param name="secondary">The secondary bound keys, if any.</param>
    /// <param name="isHeader">Whether this row is a group header rather than a bindable action.</param>
    public OptionsBindingRow(
        string action,
        string primary,
        string secondary = "",
        bool isHeader = false
    )
    {
        Action = action ?? string.Empty;
        Primary = primary ?? string.Empty;
        Secondary = secondary ?? string.Empty;
        IsHeader = isHeader;
    }

    public string Action { get; }
    public string Primary { get; }
    public string Secondary { get; }
    public bool IsHeader { get; }
}

/// <summary>
/// Represents one row in the Save/Load menu.
/// </summary>
public sealed class OptionsSaveSlot
{
    /// <summary>
    /// Creates a save row.
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

    public string Name { get; }
    public string Date { get; }
    public Texture2D FactionIcon { get; }
    public bool IsCreateNew { get; }
    public string FileName { get; }
}

/// <summary>
/// Contains the data displayed by the Options menu.
/// </summary>
public sealed class OptionsMenuRenderData
{
    /// <summary>
    /// Creates the Options menu data.
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

    public int X { get; }
    public int Y { get; }

    public OptionsMenuTab ActiveTab { get; }
    public int SelectedSlot { get; }
    public int ListeningRow { get; }
    public bool ListeningSecondary { get; }

    public string ResolutionLabel { get; }
    public string FullScreenLabel { get; }
    public IReadOnlyDictionary<UserTacticalOption, bool> TacticalStates { get; }

    public IReadOnlyList<float> Volumes { get; }

    public IReadOnlyList<OptionsBindingRow> Bindings { get; }
    public IReadOnlyList<OptionsSaveSlot> SaveSlots { get; }

    public bool CanSave { get; }
    public bool CanLoad { get; }
    public bool CanReturnToGame { get; }
    public bool CanReturnToMainMenu { get; }
}
