using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Xml;
using System.Xml.Serialization;
using System;
using UnityEngine;

class ResourceManagerImpl : IResourceManager
{
    /// <summary>
    ///
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public T GetConfig<T>()
        where T : IConfig
    {
        string configName = typeof(T).ToString();
        string filePath = Path.Combine("Configs", configName);

        string json = Resources.Load<TextAsset>(filePath).text;
        T config = JsonUtility.FromJson<T>(json);

        return config;
    }

    /// <summary>
    ///
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public T[] GetGameNodeData<T>()
        where T : GameNode
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

public class ResourceManager
{
    private static IResourceManager _resourceManager = new ResourceManagerImpl();

    /// <summary>
    ///
    /// </summary>
    public static IResourceManager Instance
    {
        get { return _resourceManager; }
    }
}
