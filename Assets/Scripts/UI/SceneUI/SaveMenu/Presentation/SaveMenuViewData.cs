using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine;

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
        IReadOnlyDictionary<UserTacticalOption, bool> tacticalOptions,
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

        TacticalOptions = new ReadOnlyDictionary<UserTacticalOption, bool>(
            new Dictionary<UserTacticalOption, bool>(tacticalOptions)
        );
        Slots = new List<SaveSlotRenderData>(slots).AsReadOnly();
        ConfirmationMessage = confirmationMessage;
    }

    public Texture2D ReturnStrategyButtonUpTexture { get; }

    public Texture2D ReturnStrategyButtonDownTexture { get; }

    public float MusicVolume { get; }

    public float SfxVolume { get; }

    public string VersionText { get; }

    public IReadOnlyDictionary<UserTacticalOption, bool> TacticalOptions { get; }

    public IReadOnlyList<SaveSlotRenderData> Slots { get; }

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

    public int Slot { get; }

    public string Label { get; }

    public bool CanSave { get; }

    public bool CanLoad { get; }

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
