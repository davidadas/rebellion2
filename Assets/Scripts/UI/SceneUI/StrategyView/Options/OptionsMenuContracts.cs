using System.Collections.Generic;

/// <summary>
/// Defines the game actions used by the Options menu.
/// </summary>
public interface IOptionsMenuHostActions
{
    bool CanReturnToGame { get; }

    bool CanReturnToMainMenu { get; }

    void PauseForOptions();

    void ResumeFromOptions();

    void ReturnToMainMenu();

    void QuitApplication();
}

/// <summary>
/// Defines the save game actions used by the Options menu.
/// </summary>
public interface IOptionsSaveStore
{
    IReadOnlyList<OptionsSaveSlot> GetSaveSlots();

    bool LoadSave(string fileName);

    void DeleteSave(string fileName);

    void RenameSave(string fileName, string displayName);
}

/// <summary>
/// Defines the save writing actions used by the Options menu.
/// </summary>
public interface IOptionsSaveWriter
{
    void CreateNamedSave(string displayName);

    void OverwriteSave(string fileName, string displayName);
}
