using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Xml;
using System.Xml.Serialization;
using System;
using UnityEngine;

/// <summary>
///
/// </summary>
public interface IAssetManager
{
    /// <summary>
    ///
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public T GetConfig<T>()
        where T : IConfig;

    /// <summary>
    /// 
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public T[] GetSceneNodeData<T>()
        where T : SceneNode;
}

/// <summary>
/// The AssetManagerImpl class is responsible for managing the retrieval of 
/// configuration data and scene node data from files in the Resources directory.
/// </summary>
class AssetManagerImpl : IAssetManager
{
    /// <summary>
    /// Retrieves the configuration data of type T.
    /// </summary>
    /// <typeparam name="T">The type of the configuration data.</typeparam>
    /// <returns>The configuration data of type T.</returns>
    public T GetConfig<T>()
        where T : IConfig
    {
        string configName = typeof(T).ToString();
        string filePath = Path.Combine("Configs", configName);

        string json = Resources.Load<TextAsset>(filePath).text;
        T config = JsonUtility.FromJson<T>(json);

        return config;
    }

    // <summary>
    /// Retrieves an array of scene node data of type T.
    /// </summary>
    /// <typeparam name="T">The type of the scene node data.</typeparam>
    /// <returns>An array of scene node data of type T.</returns>
    public T[] GetSceneNodeData<T>()
        where T : SceneNode
    {
        // Pluralize the string (e.g. Building -> Buildings).
        string pluralizedType = $"{typeof(T).ToString()}s";
        string filePath = Path.Combine("Data", pluralizedType);

        // Initialize the XML Serializer.
        // Then, load the XML from the Resources directory.
        XmlRootAttribute rootElement = new XmlRootAttribute();
        rootElement.ElementName = pluralizedType;
        XmlSerializer serializer = new XmlSerializer(typeof(T[]), rootElement);
        TextAsset gameXml = Resources.Load<TextAsset>(filePath);

        // Deserialize the result.
        using (MemoryStream stream = new MemoryStream(gameXml.bytes))
        {
            T[] gameData = serializer.Deserialize(stream) as T[];
            return gameData;
        }
    }
}

/// <summary>
/// The AssetManager class is a singleton class that provides a single point of access to the AssetManagerImpl class.
/// </summary>
public class AssetManager
{
    private static IAssetManager _resourceManager = new AssetManagerImpl();

    /// <summary>
    ///
    /// </summary>
    public static IAssetManager Instance
    {
        get { return _resourceManager; }
    }
}
