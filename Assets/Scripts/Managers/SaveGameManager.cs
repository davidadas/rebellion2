using System.IO;
using UnityEngine;

/// <summary>
/// The SaveGameManager is responsible for managing the saving and loading of the game state.
/// </summary>
public class SaveGameManager
{
    // Singleton instance.
    private static SaveGameManager instance;

    // Initialize singleton.
    public static SaveGameManager Instance
    {
        get
        {
            if (instance == null)
            {
                instance = new SaveGameManager();
            }
            return instance;
        }
    }

    /// <summary>
    /// Get the directory path for saving game data.
    /// </summary>
    /// <returns>The directory path for saving game data.</returns>
    public string GetSaveDirectoryPath()
    {
        return Path.Combine(Application.persistentDataPath, "saves");
    }

    /// <summary>
    /// Get the full path to the save file.
    /// </summary>
    /// <param name="fileName">The name of the save file.</param>
    /// <returns>The full path to the save file.</returns>
    public string GetSaveFilePath(string fileName)
    {
        return Path.Combine(Application.persistentDataPath, "saves", $"{fileName}.sav");
    }

    /// <summary>
    /// Save game data to file using XML serialization.
    /// </summary>
    /// <param name="game">The game data to save.</param>
    /// <param name="fileName">The name of the save file.</param>
    public void SaveGameData(Game game, string fileName)
    {
        string saveDirectory = GetSaveDirectoryPath();

        // Create save directory if it does not exist.
        if (!Directory.Exists(saveDirectory))
        {
            Directory.CreateDirectory(saveDirectory);
        }

        // Serialize the data to a file.
        string saveFilePath = GetSaveFilePath(fileName);
        GameSerializer serializer = new GameSerializer(typeof(Game));
        using (FileStream fileStream = new FileStream(saveFilePath, FileMode.Create))
        {
            serializer.Serialize(fileStream, game);
        }
    }

    /// <summary>
    /// Load game data from file using XML deserialization.
    /// </summary>
    /// <param name="fileName">The name of the save file.</param>
    /// <returns>The loaded game data.</returns>
    public Game LoadGameData(string fileName)
    {
        string saveFilePath = GetSaveFilePath(fileName);

        // Deserialize the data from a file.
        GameSerializer serializer = new GameSerializer(typeof(Game));
        using (FileStream fileStream = new FileStream(saveFilePath, FileMode.Open))
        {
            Game game = (Game)serializer.Deserialize(fileStream);

            return game;
        }
    }
}
