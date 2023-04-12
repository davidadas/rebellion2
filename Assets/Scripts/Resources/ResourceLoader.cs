using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using System.Xml.Serialization;
using System;
using UnityEngine;

/// <summary>
/// A simple class to manage file load operations.
/// </summary>
public class ResourceLoader
{
    /// <summary>
    /// Retrieves and serializes data from an XML file.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="filePath"></param>
    /// <param name="rootElementName"></param>
    /// <returns></returns>
    public static T FromXml<T>(string filePath, string rootElementName)
    {
        XmlRootAttribute rootElement = new XmlRootAttribute();
        rootElement.ElementName = rootElementName;
        XmlSerializer serializer = new XmlSerializer(typeof(T), rootElement);

        using (FileStream stream = new FileStream(filePath, FileMode.Open))
        {
            return (T)serializer.Deserialize(stream);
        }
    }
}
