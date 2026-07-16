using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine;

/// <summary>
/// Identifies a tactical presentation option shown by the save menu.
/// </summary>
public enum SaveMenuTacticalOption
{
    /// <summary>
    /// Controls starfield presentation.
    /// </summary>
    Starfield,

    /// <summary>
    /// Controls planet presentation.
    /// </summary>
    Planet,

    /// <summary>
    /// Controls pyrotechnic presentation.
    /// </summary>
    Pyro,

    /// <summary>
    /// Controls high-detail presentation.
    /// </summary>
    HighDetail,

    /// <summary>
    /// Controls holocube presentation.
    /// </summary>
    Holocube,
}

/// <summary>
/// Contains the presentation data for the complete save menu window.
/// </summary>
public sealed class SaveMenuWindowRenderData
{
    /// <summary>
    /// Creates immutable presentation data for the save menu window.
    /// </summary>
    /// <param name="returnStrategyButtonUpTexture">The normal return-button texture.</param>
    /// <param name="returnStrategyButtonDownTexture">The pressed return-button texture.</param>
    /// <param name="musicVolume">The normalized music volume.</param>
    /// <param name="sfxVolume">The normalized sound-effect volume.</param>
    /// <param name="versionText">The displayed application version.</param>
    /// <param name="tacticalOptions">The tactical option states.</param>
    /// <param name="slots">The save-slot presentation data.</param>
    /// <param name="confirmationMessage">The active confirmation message, or null.</param>
    public SaveMenuWindowRenderData(
        Texture2D returnStrategyButtonUpTexture,
        Texture2D returnStrategyButtonDownTexture,
        float musicVolume,
        float sfxVolume,
        string versionText,
        IReadOnlyDictionary<SaveMenuTacticalOption, bool> tacticalOptions,
        IReadOnlyList<SaveSlotRenderData> slots,
        string confirmationMessage
    )
    {
        ReturnStrategyButtonUpTexture = returnStrategyButtonUpTexture;
        ReturnStrategyButtonDownTexture = returnStrategyButtonDownTexture;
        MusicVolume = Mathf.Clamp01(musicVolume);
        SfxVolume = Mathf.Clamp01(sfxVolume);
        VersionText = versionText ?? string.Empty;
        if (tacticalOptions == null)
            throw new ArgumentNullException(nameof(tacticalOptions));
        if (slots == null)
            throw new ArgumentNullException(nameof(slots));

        TacticalOptions = new ReadOnlyDictionary<SaveMenuTacticalOption, bool>(
            new Dictionary<SaveMenuTacticalOption, bool>(tacticalOptions)
        );
        Slots = new List<SaveSlotRenderData>(slots).AsReadOnly();
        ConfirmationMessage = confirmationMessage;
    }

    /// <summary>
    /// Gets the normal return-to-strategy button texture.
    /// </summary>
    public Texture2D ReturnStrategyButtonUpTexture { get; }

    /// <summary>
    /// Gets the pressed return-to-strategy button texture.
    /// </summary>
    public Texture2D ReturnStrategyButtonDownTexture { get; }

    /// <summary>
    /// Gets the normalized music volume.
    /// </summary>
    public float MusicVolume { get; }

    /// <summary>
    /// Gets the normalized sound-effect volume.
    /// </summary>
    public float SfxVolume { get; }

    /// <summary>
    /// Gets the displayed application version.
    /// </summary>
    public string VersionText { get; }

    /// <summary>
    /// Gets the tactical option states.
    /// </summary>
    public IReadOnlyDictionary<SaveMenuTacticalOption, bool> TacticalOptions { get; }

    /// <summary>
    /// Gets the ordered save-slot presentation data.
    /// </summary>
    public IReadOnlyList<SaveSlotRenderData> Slots { get; }

    /// <summary>
    /// Gets the active confirmation message, or null when no confirmation is shown.
    /// </summary>
    public string ConfirmationMessage { get; }
}

/// <summary>
/// Contains the presentation data for one save slot.
/// </summary>
public sealed class SaveSlotRenderData
{
    /// <summary>
    /// Creates immutable presentation data for one save slot.
    /// </summary>
    /// <param name="slot">The zero-based save-slot index.</param>
    /// <param name="label">The displayed save name.</param>
    /// <param name="canSave">Whether the slot accepts save requests.</param>
    /// <param name="canLoad">Whether the slot accepts load requests.</param>
    /// <param name="factionIconTexture">The faction icon for the saved game.</param>
    public SaveSlotRenderData(
        int slot,
        string label,
        bool canSave,
        bool canLoad,
        Texture2D factionIconTexture
    )
    {
        Slot = slot;
        Label = label ?? string.Empty;
        CanSave = canSave;
        CanLoad = canLoad;
        FactionIconTexture = factionIconTexture;
    }

    /// <summary>
    /// Gets the zero-based save-slot index.
    /// </summary>
    public int Slot { get; }

    /// <summary>
    /// Gets the displayed save name.
    /// </summary>
    public string Label { get; }

    /// <summary>
    /// Gets whether the slot accepts save requests.
    /// </summary>
    public bool CanSave { get; }

    /// <summary>
    /// Gets whether the slot accepts load requests.
    /// </summary>
    public bool CanLoad { get; }

    /// <summary>
    /// Gets the faction icon for the saved game.
    /// </summary>
    public Texture2D FactionIconTexture { get; }

    /// <summary>
    /// Creates disabled presentation data for an empty slot.
    /// </summary>
    /// <param name="slot">The zero-based save-slot index.</param>
    /// <returns>Disabled presentation data for the requested slot.</returns>
    public static SaveSlotRenderData Empty(int slot)
    {
        return new SaveSlotRenderData(slot, string.Empty, false, false, null);
    }
}
