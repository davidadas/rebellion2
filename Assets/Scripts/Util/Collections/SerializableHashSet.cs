using System.Collections.Generic;
using System.IO;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;
using System;

[Serializable]
public class SerializableHashSet<T> : HashSet<T>, IXmlSerializable
{
    // No need for additional members since we inherit from HashSet<T>

    public XmlSchema GetSchema() => null; // No schema required

    /// <summary>
    /// Reads the XML representation of the object from the provided reader.
    /// </summary>
    /// <param name="reader">The <see cref="XmlReader"/> stream from which the object is deserialized.</param>
    public void ReadXml(XmlReader reader)
    {
        XmlSerializer serializer = new XmlSerializer(typeof(T));
        reader.ReadStartElement();

        while (reader.NodeType != XmlNodeType.EndElement)
        {
            T item = (T)serializer.Deserialize(reader);
            this.Add(item);
        }

        reader.ReadEndElement();
    }

    /// <summary>
    /// Converts the object into its XML representation and writes it to the provided writer.
    /// </summary>
    /// <param name="writer">The <see cref="XmlWriter"/> stream to which the object is serialized.</param>
    public void WriteXml(XmlWriter writer)
    {
        XmlSerializer serializer = new XmlSerializer(typeof(T));

        foreach (T item in this)
        {
            serializer.Serialize(writer, item);
        }
    }
}
