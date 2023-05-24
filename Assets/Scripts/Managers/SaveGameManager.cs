using System.IO;
using System.Xml.Serialization;
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
    /// Save game data to file using XML serialization.
    /// </summary>
    /// <param name="game"></param>
    /// <param name="fileName"></param>
    public void SaveGameData(Game game, string fileName)
    {
        string saveDirectory = Path.Combine(Application.persistentDataPath, "saves");
        string saveFilePath = Path.Combine(saveDirectory, $"{fileName}.sav");

        // Create save directory if it does not exist.
        if (!Directory.Exists(saveDirectory))
        {
            Directory.CreateDirectory(saveDirectory);
        }

        // Serialize the data to a file.
        XmlSerializer serializer = new XmlSerializer(typeof(Game));
        using (FileStream fileStream = new FileStream(saveFilePath, FileMode.Create))
        {
            serializer.Serialize(fileStream, game);
        }
    }

    /// <summary>
    /// Load game data from file using XML deserialization.
    /// </summary>
    /// <param name="fileName"></param>
    /// <returns></returns>
    public Game LoadGameData(string fileName)
    {
        string saveFilePath = Path.Combine(
            Application.persistentDataPath,
            "saves",
            $"{fileName}.sav"
        );

        // Deserialize the data from a file.
        XmlSerializer serializer = new XmlSerializer(typeof(Game));
        using (FileStream fileStream = new FileStream(saveFilePath, FileMode.Open))
        {
            return (Game)serializer.Deserialize(fileStream);
        }
    }
}
