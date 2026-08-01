using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game;
using UnityEngine;

/// <summary>
/// Projects save-game, theme, and option state into immutable save-menu presentation data.
/// </summary>
public sealed class SaveMenuDataBuilder
{
    private readonly FactionThemeLibrary themeLibrary;
    private readonly SaveGameManager saveGameManager;
    private readonly Func<string, Texture2D> loadTexture;
    private readonly Func<GameMetadata, bool> canLoadSave;
    private readonly Dictionary<string, Texture2D> texturesByPath = new Dictionary<
        string,
        Texture2D
    >(StringComparer.Ordinal);
    private readonly string versionText;

    /// <summary>
    /// Creates a save-menu presentation builder with explicit data and asset dependencies.
    /// </summary>
    /// <param name="themeLibrary">The faction-theme source.</param>
    /// <param name="saveGameManager">The save-slot source.</param>
    /// <param name="loadTexture">The texture resolver.</param>
    /// <param name="canLoadSave">The active content compatibility check.</param>
    /// <param name="versionText">The application version label.</param>
    public SaveMenuDataBuilder(
        FactionThemeLibrary themeLibrary,
        SaveGameManager saveGameManager,
        Func<string, Texture2D> loadTexture,
        Func<GameMetadata, bool> canLoadSave,
        string versionText
    )
    {
        this.themeLibrary = themeLibrary ?? throw new ArgumentNullException(nameof(themeLibrary));
        this.saveGameManager =
            saveGameManager ?? throw new ArgumentNullException(nameof(saveGameManager));
        this.loadTexture = loadTexture ?? throw new ArgumentNullException(nameof(loadTexture));
        this.canLoadSave = canLoadSave ?? throw new ArgumentNullException(nameof(canLoadSave));
        this.versionText = versionText ?? string.Empty;
    }

    /// <summary>
    /// Builds the complete save-menu presentation for the current scene state.
    /// </summary>
    /// <param name="canSave">Whether the active game may be saved.</param>
    /// <param name="musicVolume">The normalized music volume.</param>
    /// <param name="sfxVolume">The normalized sound-effect volume.</param>
    /// <param name="tacticalOptions">The tactical option states.</param>
    /// <param name="confirmationMessage">The active confirmation message, or null.</param>
    /// <param name="saves">The already-loaded save metadata entries.</param>
    /// <returns>Immutable presentation data for the save-menu window.</returns>
    public SaveMenuWindowRenderData CreateRenderData(
        bool canSave,
        float musicVolume,
        float sfxVolume,
        IReadOnlyDictionary<UserTacticalOption, bool> tacticalOptions,
        string confirmationMessage,
        IReadOnlyList<SaveGameEntry> saves
    )
    {
        if (saves == null)
            throw new ArgumentNullException(nameof(saves));

        return new SaveMenuWindowRenderData(
            musicVolume,
            sfxVolume,
            versionText,
            tacticalOptions,
            BuildSaveMenuSlots(canSave, saves),
            confirmationMessage
        );
    }

    /// <summary>
    /// Builds presentation data for every configured save slot.
    /// </summary>
    /// <param name="canSave">Whether save commands are currently enabled.</param>
    /// <param name="saves">The already-loaded save metadata entries.</param>
    /// <returns>Presentation data ordered by save-slot index.</returns>
    private IReadOnlyList<SaveSlotRenderData> BuildSaveMenuSlots(
        bool canSave,
        IReadOnlyList<SaveGameEntry> saves
    )
    {
        Dictionary<string, SaveGameEntry> savesByFileName = saves.ToDictionary(save =>
            save.FileName
        );
        List<SaveSlotRenderData> slots = new List<SaveSlotRenderData>(
            saveGameManager.SaveSlotCount
        );

        for (int slot = 0; slot < saveGameManager.SaveSlotCount; slot++)
        {
            string fileName = saveGameManager.GetSaveSlotFileName(slot);
            savesByFileName.TryGetValue(fileName, out SaveGameEntry save);
            bool canLoad = save != null && canLoadSave(save.Metadata);
            slots.Add(
                new SaveSlotRenderData(
                    slot,
                    GetSaveSlotLabel(save, slot),
                    canSave,
                    canLoad,
                    GetSaveSlotFactionIcon(save, canLoad)
                )
            );
        }

        return slots;
    }

    /// <summary>
    /// Resolves a saved display name with the configured slot label as its fallback.
    /// </summary>
    /// <param name="save">The discovered save entry, or null.</param>
    /// <param name="slot">The zero-based save-slot index.</param>
    /// <returns>The displayed save name.</returns>
    private string GetSaveSlotLabel(SaveGameEntry save, int slot)
    {
        if (save == null)
            return string.Empty;
        if (!string.IsNullOrEmpty(save.Metadata?.SaveDisplayName))
            return save.Metadata.SaveDisplayName;

        return saveGameManager.GetSaveSlotDisplayName(slot);
    }

    /// <summary>
    /// Resolves the faction icon configured for a discovered save.
    /// </summary>
    /// <param name="save">The discovered save entry, or null.</param>
    /// <param name="canLoad">Whether the save matches the active content.</param>
    /// <returns>The configured faction icon texture, or null.</returns>
    private Texture2D GetSaveSlotFactionIcon(SaveGameEntry save, bool canLoad)
    {
        if (!canLoad)
            return null;

        string factionId = save?.Metadata?.PlayerFactionID;
        return string.IsNullOrEmpty(factionId)
            ? null
            : GetTexture(GetTheme(factionId)?.SaveMenuSlotIconImagePath);
    }

    /// <summary>
    /// Resolves a configured faction theme while preserving default-theme fallback behavior.
    /// </summary>
    /// <param name="factionId">The faction identifier.</param>
    /// <returns>The matching or default theme.</returns>
    private FactionTheme GetTheme(string factionId)
    {
        return themeLibrary.GetTheme(factionId);
    }

    /// <summary>
    /// Resolves an optional configured texture path.
    /// </summary>
    /// <param name="path">The resource texture path.</param>
    /// <returns>The resolved texture, or null for an empty path.</returns>
    private Texture2D GetTexture(string path)
    {
        if (string.IsNullOrEmpty(path))
            return null;
        if (texturesByPath.TryGetValue(path, out Texture2D texture))
            return texture;

        texture = loadTexture(path);
        if (texture != null)
            texturesByPath.Add(path, texture);
        return texture;
    }
}
