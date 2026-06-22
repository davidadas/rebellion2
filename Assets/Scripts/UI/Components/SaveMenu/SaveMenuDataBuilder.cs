using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public sealed class SaveMenuDataBuilder
{
    private const int _slotCount = 6;
    private const string _slotFilePrefix = "save_slot_";
    private const string _emptySaveSlotLabel = "Empty Save Slot";

    private readonly FactionThemeLibrary themeLibrary = new FactionThemeLibrary();
    private readonly Dictionary<string, Texture2D> texturesByPath =
        new Dictionary<string, Texture2D>();

    public SaveMenuWindowRenderData CreateRenderData(
        int x,
        int y,
        string playerFactionId,
        bool canSave
    )
    {
        return new SaveMenuWindowRenderData
        {
            X = x,
            Y = y,
            ReturnStrategyButtonUpTexture = GetReturnStrategyButtonTexture(playerFactionId),
            MusicVolume = AudioManager.Instance?.musicVolume ?? 0f,
            SfxVolume = AudioManager.Instance?.sfxVolume ?? 1f,
            VersionText = GetVersionText(),
            Slots = BuildSaveMenuSlots(canSave),
        };
    }

    private List<SaveSlotRenderData> BuildSaveMenuSlots(bool canSave)
    {
        IReadOnlyList<SaveGameEntry> saves = SaveGameManager.Instance.GetSavedGames();
        Dictionary<string, SaveGameEntry> savesByFileName = saves.ToDictionary(save =>
            save.FileName
        );
        List<SaveSlotRenderData> slots = new List<SaveSlotRenderData>(_slotCount);

        for (int slot = 0; slot < _slotCount; slot++)
        {
            string fileName = GetSaveSlotFileName(slot);
            savesByFileName.TryGetValue(fileName, out SaveGameEntry save);
            slots.Add(
                new SaveSlotRenderData
                {
                    Slot = slot,
                    Label = save == null ? _emptySaveSlotLabel : GetSaveSlotLabel(save, slot),
                    CanSave = canSave,
                    CanLoad = save != null,
                    FactionIconTexture = GetSaveSlotFactionIcon(save),
                }
            );
        }

        return slots;
    }

    public static bool IsValidSaveSlot(int slot)
    {
        return slot >= 0 && slot < _slotCount;
    }

    public static string GetSaveSlotFileName(int slot)
    {
        return _slotFilePrefix + (slot + 1);
    }

    public static string GetSaveSlotDisplayName(int slot)
    {
        return "Save Slot " + (slot + 1);
    }

    private static string GetSaveSlotLabel(SaveGameEntry save, int slot)
    {
        if (!string.IsNullOrEmpty(save?.Metadata?.SaveDisplayName))
            return save.Metadata.SaveDisplayName;

        return GetSaveSlotDisplayName(slot);
    }

    private Texture2D GetSaveSlotFactionIcon(SaveGameEntry save)
    {
        string path = GetSaveSlotFactionIconPath(save?.Metadata?.PlayerFactionID);
        if (string.IsNullOrEmpty(path))
            return null;

        return GetTexture(path);
    }

    private Texture2D GetReturnStrategyButtonTexture(string factionInstanceId)
    {
        string path = GetReturnStrategyButtonTexturePath(factionInstanceId);
        if (string.IsNullOrEmpty(path))
            return null;

        return GetTexture(path);
    }

    private Texture2D GetTexture(string path)
    {
        if (!texturesByPath.TryGetValue(path, out Texture2D texture))
        {
            texture = ResourceManager.TryGetTexture(path);
            texturesByPath[path] = texture;
        }

        return texture;
    }

    private string GetSaveSlotFactionIconPath(string factionInstanceId)
    {
        if (string.IsNullOrEmpty(factionInstanceId))
            return null;

        return themeLibrary.GetTheme(factionInstanceId)?.SaveMenuSlotIconImagePath;
    }

    private string GetReturnStrategyButtonTexturePath(string factionInstanceId)
    {
        if (string.IsNullOrEmpty(factionInstanceId))
            return null;

        return themeLibrary.GetTheme(factionInstanceId)?.SaveMenuReturnStrategyButtonImagePath;
    }

    private static string GetVersionText()
    {
        return string.IsNullOrEmpty(Application.version)
            ? "Version: Development"
            : "Version: " + Application.version;
    }
}
