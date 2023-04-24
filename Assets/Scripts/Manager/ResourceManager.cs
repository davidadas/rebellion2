using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Xml;
using System.Xml.Serialization;
using System;
using UnityEngine;

public class ResourceManager
{
    /// <summary>
    ///
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public static T GetConfig<T>()
        where T : Config
    {
        string configName = typeof(T).ToString();

        string json = Resources.Load<TextAsset>($"Configs/{configName}").text;
        T config = JsonUtility.FromJson<T>(json);

        return config;
    }

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

        // Initialize the XML Serializer.
        // Then, load the XML from the Resources directory.
        XmlRootAttribute rootElement = new XmlRootAttribute();
        rootElement.ElementName = pluralizedType;
        XmlSerializer serializer = new XmlSerializer(typeof(T[]), rootElement);
        TextAsset gameXml = Resources.Load<TextAsset>($"Data/{pluralizedType}");

        // Deserialize the result.
        using (MemoryStream stream = new MemoryStream(gameXml.bytes))
        {
            T[] gameData = (T[])serializer.Deserialize(stream);
            return gameData;
        }
    }
}
