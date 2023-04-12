using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System;
using UnityEngine;

public class ResourceManager
{
    // @TODO: Move this to an AppConfig.
    private static readonly string _gameDataPath =
        $".{Path.DirectorySeparatorChar}Assets{Path.DirectorySeparatorChar}Data";

    /// <summary>
    ///
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public static T[] GetGameNodeData<T>()
        where T : GameNode
    {
        // Pluralize the string (e.g. Building -> Buildings).
        string pluralizedType = $"{typeof(T).ToString()}s";

        // Load XML data from a file.
        T[] gameData = ResourceLoader.FromXml<T[]>(
            $"{_gameDataPath}{Path.DirectorySeparatorChar}{pluralizedType}.xml",
            pluralizedType
        );

        return gameData;
    }
}
